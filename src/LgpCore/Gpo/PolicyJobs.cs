using System;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Xml.Linq;
using Infrastructure;
using LgpCore.AdmParser;
using LgpCore.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LgpCore.Gpo
{
  public enum PolicyValueAction
  {
    ValueShouldExist,
    SingleValueShouldExist,
    ValueShouldNotExist,
    NoValueShouldExist,
    AnyValueShouldExist,
    SetValue,
    RemoveValue,
    RemoveValues
  }

  public enum PolicyValueDeleteType
  {
    None,
    DeleteValue,
    DeleteValues
  }

  public abstract class PolicyJobBase
  {
    public PolicyState CalcPolicyState(bool enabled, bool disabled, bool notConfigured, GpoContext context)
    {
      if (enabled && !disabled && !notConfigured) return PolicyState.Enabled;
      if (!enabled && disabled && !notConfigured) return PolicyState.Disabled;
      if (!enabled && !disabled && notConfigured) return PolicyState.NotConfigured;
      return PolicyState.Suspect;
    }
}

  public class PolicyJob : PolicyJobBase
  {
    private readonly Policy policy;

    public PolicyJob(Policy policy)
    {
      this.policy = policy;

      ElementJobs = policy.Elements.Select(e => e.GetJob()).ToList();
    }

    public List<PolicyElementJobBase> ElementJobs { get; }

    public void Enable(GpoContext context, Dictionary<string, object> values)
    {
      Enable(context, policy.ToElementValues(values));
    }

    public void Enable(GpoContext context, Dictionary<PolicyElement, object> values)
    {
      context.Logger?.LogDebug($"Enable policy {policy.PrefixedName()} ({context.Class}) '{policy.Name}' ...");
      using var _ = context.InitForPolicy(policy);

      context.Class.CheckClassIsNotBoth();
      policy.CheckClass(context.Class);

      NotConfigured(context); //delete all existing values prior writing new ones

      var actions = policy.GetEnabledList().GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.None);
      actions.Execute(context);
      ElementJobs.ForEach(j => j.Enable(context, values[j.Element]));
      context.Logger?.LogDebug($"Enable policy {policy.PrefixedName()} ({context.Class}) Done.");
    }

    public void Disable(GpoContext context)
    {
      context.Logger?.LogDebug($"Disable policy {policy.PrefixedName()} ({context.Class}) '{policy.Name}' ...");
      using var _ = context.InitForPolicy(policy);

      context.Class.CheckClassIsNotBoth();
      policy.CheckClass(context.Class);

      NotConfigured(context); //delete all existing values prior writing new ones

      var enabledList = policy.GetEnabledList();
      var disabledList = policy.GetDisabledList();
      var actions = disabledList.Any()
        ? disabledList.GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.None)
        : enabledList.GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.DeleteValue);

      actions.Execute(context);
      ElementJobs.ForEach(j => j.Disable(context));
      context.Logger?.LogDebug($"Disable policy {policy.PrefixedName()} ({context.Class}) Done.");
    }

    public void NotConfigured(GpoContext context)
    {
      context.Logger?.LogDebug($"NotConfigure policy {policy.PrefixedName()} ({context.Class}) '{policy.Name}' ...");
      using var _ = context.InitForPolicy(policy);

      context.Class.CheckClassIsNotBoth();
      policy.CheckClass(context.Class);

      var enabledList = policy.GetEnabledList();
      var disabledList = policy.GetDisabledList();

      var actions = enabledList.GetActions(PolicyValueAction.RemoveValue, PolicyValueDeleteType.None);
      if (disabledList.Any())
        actions.AddRange(disabledList.GetActions(PolicyValueAction.RemoveValue, PolicyValueDeleteType.None));
      else
        actions.AddRange(enabledList.GetActions(PolicyValueAction.RemoveValue, PolicyValueDeleteType.DeleteValue));

      actions.Execute(context);
      ElementJobs.ForEach(j => j.NotConfigured(context));
      context.Logger?.LogDebug($"NotConfigure policy {policy.PrefixedName()} ({context.Class}) Done.");
    }

    public PolicyState GetState(GpoContext context)
    {
      using var _ = context.InitForPolicy(policy);
      context.Class.CheckClassIsNotBoth();
      policy.CheckClass(context.Class);

      //the local values EnabledList, DisabledList
      var result = PolicyState.Unknown;
      var enabledList = policy.GetEnabledList();
      var disabledList = policy.GetDisabledList();
      PolicyState itemsState = PolicyState.Unknown;
      context.Logger?.LogTrace($"Check enabled state of policy (simple items) {policy.PrefixedName()} {context.Class} enabledList:{enabledList.Count} disabledList:{disabledList.Count} ...");
      using (context.Logger?.BeginScope("simple items"))
      {
        if (enabledList.Any() || disabledList.Any())
        {
          //enabled?
          bool enabled, disabled, notConfigured;
          using (context.Logger?.BeginScope("Enabled"))
          {
            enabled = enabledList.GetActions(PolicyValueAction.ValueShouldExist, PolicyValueDeleteType.None).Execute(context);
          }

          //disabled?
          using (context.Logger?.BeginScope("Disabled"))
          {
            var disabledActions = disabledList.Any()
              ? disabledList.GetActions(PolicyValueAction.ValueShouldExist, PolicyValueDeleteType.None)
              : enabledList.GetActions(PolicyValueAction.ValueShouldExist, PolicyValueDeleteType.DeleteValue);
            disabled = disabledActions.Execute(context);
          }

          //notconfigured?
          using (context.Logger?.BeginScope("NotConfigured"))
          {
            var notConfiguredActions =
              enabledList.GetActions(PolicyValueAction.ValueShouldNotExist, PolicyValueDeleteType.None);
            if (disabledList.Any())
              notConfiguredActions.AddRange(disabledList.GetActions(PolicyValueAction.ValueShouldNotExist, PolicyValueDeleteType.None));
            else
              notConfiguredActions.AddRange(enabledList.GetActions(PolicyValueAction.ValueShouldNotExist, PolicyValueDeleteType.DeleteValue));
            notConfigured = notConfiguredActions.Execute(context);
          }

          itemsState = CalcPolicyState(enabled, disabled, notConfigured, context);
          context.Logger?.LogTrace($"Checking state of policy (simple items) {policy.PrefixedName()} {context.Class} -> enabled:{enabled} disabled:{disabled} notConfigured:{notConfigured} - itemsState:{itemsState}");
        }
      }

      //the PolicyElements:
      if (!policy.Elements.Any())
      {
        context.Logger?.LogDebug($"Checked state of policy {policy.PrefixedName()} {context.Class} -> {itemsState}");
        result = itemsState;
      }
      else
      { //check Elements
        PolicyState elementsState;
        var elementStates = ElementJobs
          .Select(job =>
          {
            using (context.Logger?.BeginScope($"{job.Element.GetType().Name} '{job.Element.Id}'"))
            {
              var jobStates = job.GetState(context);
              context.Logger?.LogTrace($"State: {string.Join(", ", jobStates.Select(e => e.ToString()))}");
              return (job: job, validStates: jobStates);
            }
              
          })
          .ToList();
        //why is this so complex:
        //because there might be boolean elements with the same values for enabled and disabled (false might be delete-value)
        var allStates = elementStates
          .SelectMany(e => e.validStates)
          .Distinct()
          .ToHashSet();
        var validStates = allStates
          .Select(s => (state: s, count: elementStates.Where(e => e.validStates.Contains(s)).Count()))
          .Where(e => e.count == policy.Elements.Count)
          .Select(e => e.state)
          .ToHashSet();
        if (validStates.Count == 1)
        {
          elementsState = validStates.First();
        }
        else
        {
          if (validStates.Count == 2 && validStates.Contains(PolicyState.Enabled) &&
              validStates.Contains(PolicyState.Disabled))
          {
            if (validStates.Contains(itemsState))
              elementsState = itemsState; //in this case the item's state may override
            else
              elementsState = PolicyState.Disabled; //probably we have BooleanElement(s) (without standard items) with same values for enabled (with value false) and disabled (which is interpreted same as Value false)
          }
          else
          {
            elementsState = PolicyState.Suspect;
          }
        }

        if (itemsState == PolicyState.Unknown)
        { //no items -> just take elements state
          result = elementsState;
        }
        else
        { //combine itemsState and elementsState
          result = elementsState == itemsState
            ? elementsState //items and elements have the same state
            : PolicyState.Suspect; //items and elements have different states
        }
      }

      context.Logger?.LogDebug($"Checked state of policy {policy.PrefixedName()} {context.Class} -> {result}");
      return result;
    }

    public Dictionary<PolicyElement, object?> GetValues(GpoContext context)
    {
      using var _ = context.InitForPolicy(policy);

      return ElementJobs.ToDictionary(e => e.Element, e => e.GetValue(context));
    }

    public List<(PolicyElement? element, PolicyValueItemAction action)> ReportRegistrySettings(PolicyState policyState)
    {
      //simple policy items
      var enabledList = policy.GetEnabledList();
      var disabledList = policy.GetDisabledList();

      List<PolicyValueItemAction> actions;
      switch (policyState)
      {
        case PolicyState.Enabled:
          actions = enabledList.GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.None);
          break;
        case PolicyState.Disabled:
          actions = disabledList.Any()
            ? disabledList.GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.None)
            : enabledList.GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.DeleteValue);
          break;
        case PolicyState.NotConfigured:
          actions = enabledList.GetActions(PolicyValueAction.RemoveValue, PolicyValueDeleteType.None);
          if (disabledList.Any())
            actions.AddRange(disabledList.GetActions(PolicyValueAction.RemoveValue, PolicyValueDeleteType.None));
          else
            actions.AddRange(enabledList.GetActions(PolicyValueAction.RemoveValue, PolicyValueDeleteType.DeleteValue));
          break;

        case PolicyState.Unknown:
        case PolicyState.Suspect:
        default:
          throw new ArgumentOutOfRangeException(nameof(policyState), policyState, null);
      }

      var result = actions
        .Select(a => (element: (PolicyElement?)null, action: a))
        .ToList();

      //elements
      foreach (var elementJob in ElementJobs)
      {
        result.AddRange(elementJob.ReportRegistrySettings(policyState)
          .Select(a => (element: (PolicyElement?)elementJob.Element, action: a)));
      }

      return result;
    }
  }

  public abstract class PolicyElementJobBase : PolicyJobBase
  {
    public PolicyElement Element { get; }

    public PolicyElementJobBase(PolicyElement policyElement)
    {
      Element = policyElement;
    }

    public string RegKey => Element.RegKey ?? Element.Parent.RegKey ?? throw new InvalidOperationException("No RegKey defined");
    public string RegValueName => Element is PolicyElementBase policyElementBase
      ? policyElementBase.RegValueName ?? Element.Parent.RegValueName ?? throw new InvalidOperationException("No RegValueName defined")
      : throw new InvalidOperationException($"{Element.GetType().Name} does not implement RegValueName");
    public abstract RegistryValueKind ValueKind { get; }
    public abstract Type ValueType { get; }
    public virtual bool Enable(GpoContext context, object value)
    {
      if (value == null)
        throw new InvalidOperationException("Value is null");

      if (!value.GetType().Equals(ValueType))
        throw new InvalidOperationException($"{Element.GetType().Name} {Element.Id} Value shall be '{ValueType.Name}' is '{value.GetType().Name}'");

      value = ValueToRegValue(value);

      var action = new PolicyValueItemAction(
        PolicyValueAction.SetValue,
        RegKey,
        RegValueName,
        ValueKind,
        value,
        PolicyValueDeleteType.None);

      return action.Execute(context);
    }

    public virtual bool Disable(GpoContext context)
    {
      var action = new PolicyValueItemAction(
        PolicyValueAction.SetValue,
        RegKey,
        RegValueName,
        RegistryValueKind.None,
        null,
        PolicyValueDeleteType.DeleteValue);

      return action.Execute(context);
    }

    public virtual bool NotConfigured(GpoContext context)
    {
      var action1 = new PolicyValueItemAction(
        PolicyValueAction.RemoveValue,
        RegKey,
        RegValueName,
        RegistryValueKind.None,
        null,
        PolicyValueDeleteType.None);
      var action2 = new PolicyValueItemAction(
        PolicyValueAction.RemoveValue,
        RegKey,
        RegValueName,
        RegistryValueKind.None,
        null,
        PolicyValueDeleteType.DeleteValue);

      var actions = new List<PolicyValueItemAction>()
        {
          action1,
          action2
        };

      return actions.Execute(context);
    }

    public virtual HashSet<PolicyState> GetState(GpoContext context)
    {
      var enabled = new PolicyValueItemAction(
        PolicyValueAction.ValueShouldExist,
        RegKey,
        RegValueName,
        ValueKind,
        null,
        PolicyValueDeleteType.None).Execute(context);
      var disabled = new PolicyValueItemAction(
        PolicyValueAction.ValueShouldExist,
        RegKey,
        RegValueName,
        RegistryValueKind.None,
        null,
        PolicyValueDeleteType.DeleteValue).Execute(context);
      var notConfigured = new List<PolicyValueItemAction>()
      {
        new PolicyValueItemAction(
          PolicyValueAction.ValueShouldNotExist,
          RegKey,
          RegValueName,
          RegistryValueKind.None,
          null,
          PolicyValueDeleteType.None),
        new PolicyValueItemAction(
          PolicyValueAction.ValueShouldNotExist,
          RegKey,
          RegValueName,
          RegistryValueKind.None,
          null,
          PolicyValueDeleteType.DeleteValue)
      }
        .Execute(context);

      return new HashSet<PolicyState>() { CalcPolicyState(enabled, disabled, notConfigured, context) };
    }

    protected object ValueToRegValue(object o)
    {
      switch (ValueKind)
      {
        //case RegistryValueKind.None:
        //case RegistryValueKind.Unknown:
        case RegistryValueKind.String:
        case RegistryValueKind.ExpandString:
          return Convert.ChangeType(o, TypeCode.String);
        case RegistryValueKind.MultiString:
          return o;
        //case RegistryValueKind.Binary:
        case RegistryValueKind.DWord:
          return Convert.ChangeType(o, TypeCode.UInt32);
        case RegistryValueKind.QWord:
          return Convert.ChangeType(o, TypeCode.UInt64);
        default:
          return o;
      }
    }

    protected object? RegValueToValue(object? o)
    {
      if (o == null)
        return null;
      var typeCode = Type.GetTypeCode(ValueType);
      if (typeCode != TypeCode.Object)
        return Convert.ChangeType(o, typeCode);
      return o;
    }

    public virtual object? GetValue(GpoContext context)
    {
      context.SwitchToLocalRegKey(RegKey, false);
      var value = context.GetValue(RegValueName, out var valueKind);
      if (valueKind != ValueKind)
        return null;
      value = RegValueToValue(value);
      if (value?.GetType() == ValueType)
      {
        return value;
      }
      return null;
    }

    public virtual List<PolicyValueItemAction> ReportRegistrySettings(PolicyState policyState)
    {
      List<PolicyValueItemAction> actions;
      switch (policyState)
      {
        case PolicyState.Enabled:
          actions = new List<PolicyValueItemAction>()
          {
            new PolicyValueItemAction(
              PolicyValueAction.SetValue,
              RegKey,
              RegValueName,
              ValueKind,
              null,
              PolicyValueDeleteType.None)
          };
          break;
        case PolicyState.Disabled:
          actions = new List<PolicyValueItemAction>()
          {
            new PolicyValueItemAction(
              PolicyValueAction.SetValue,
              RegKey,
              RegValueName,
              RegistryValueKind.None,
              null,
              PolicyValueDeleteType.DeleteValue)
          };
        break;
        case PolicyState.NotConfigured:
          actions = new List<PolicyValueItemAction>()
          {
            new PolicyValueItemAction(
              PolicyValueAction.RemoveValue,
              RegKey,
              RegValueName,
              RegistryValueKind.None,
              null,
              PolicyValueDeleteType.None),
            new PolicyValueItemAction(
              PolicyValueAction.RemoveValue,
              RegKey,
              RegValueName,
              RegistryValueKind.None,
              null,
              PolicyValueDeleteType.DeleteValue)
          };
          break;

        case PolicyState.Unknown:
        case PolicyState.Suspect:
        default:
          throw new ArgumentOutOfRangeException(nameof(policyState), policyState, null);
      }

      return actions;
    }
  }

  public class PolicyEnumElementJob : PolicyElementJobBase
  {
    public EnumElement EnumElement => (EnumElement)Element;
    public PolicyEnumElementJob(EnumElement policyElement) : base(policyElement)
    {
    }

    public override RegistryValueKind ValueKind => RegistryValueKind.String;
    public override Type ValueType => typeof(string);

    public override bool Enable(GpoContext context, object value)
    {
      //the data for the Enum is the 'DisplayName' without "$(string. )"
      if (value is not string sEnumValue)
        throw new InvalidOperationException("Value is not a string");
      var enumItem = EnumElement.GetItem(sEnumValue, false);
      if (enumItem == null)
        throw new InvalidOperationException($"EnumItem '{sEnumValue}' not found, possible values: {string.Join(", ", EnumElement.Items.Select(e => e.Id()))}");

      var actions = enumItem.Values.GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.None);

      return actions.Execute(context);
    }

    public override bool Disable(GpoContext context)
    {
      var actions = EnumElement.Items.SelectMany(ei => ei.Values.GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.DeleteValue)).ToList();

      return actions.Execute(context);
    }

    public override bool NotConfigured(GpoContext context)
    {
      var actions = EnumElement.Items.SelectMany(ei => ei.Values.GetActions(PolicyValueAction.RemoveValue, PolicyValueDeleteType.None))
        .Concat(EnumElement.Items.SelectMany(ei => ei.Values.GetActions(PolicyValueAction.RemoveValue, PolicyValueDeleteType.DeleteValue)))
        .ToList();

      return actions.Execute(context);
    }

    public override HashSet<PolicyState> GetState(GpoContext context)
    {
      bool enabled;
      using (context.Logger?.BeginScope($"Enabled"))
      {
        var enableds = EnumElement.Items
          .Select(e =>
          {
            using (context.Logger?.BeginScope($"EnumItem: {e.Id()}"))
            {
              return (allSuccess: e.Values.GetActions(PolicyValueAction.ValueShouldExist, PolicyValueDeleteType.None)
                .Execute(context), sEnumValue: e.Id());
            }

          })
          .ToList();
        var countEnabled = enableds.Count(e => e.allSuccess);
        if (countEnabled > 1)
          return new HashSet<PolicyState>() {PolicyState.Suspect};
        enabled = countEnabled == 1;
      }

      bool disabled;
      using (context.Logger?.BeginScope($"Disabled"))
      {
        disabled = EnumElement.Items
          .SelectMany(e => e.Values.GetActions(PolicyValueAction.ValueShouldExist, PolicyValueDeleteType.DeleteValue))
          .ToList().Execute(context);
      }

      bool notConfigured;
      using (context.Logger?.BeginScope($"NotConfigured"))
      {

        notConfigured = EnumElement.Items
                          .SelectMany(e =>
                            e.Values.GetActions(PolicyValueAction.ValueShouldNotExist, PolicyValueDeleteType.None))
                          .ToList().Execute(context) &&
                        EnumElement.Items.SelectMany(e => e.Values.GetActions(PolicyValueAction.ValueShouldNotExist,
                            PolicyValueDeleteType.DeleteValue))
                          .ToList()
                          .Execute(context);
      }

      return new HashSet<PolicyState> { CalcPolicyState(enabled, disabled, notConfigured, context) };
    }

    public override object? GetValue(GpoContext context)
    {
      var enableds = EnumElement.Items
        .Select(e => (allSuccess: e.Values.GetActions(PolicyValueAction.ValueShouldExist, PolicyValueDeleteType.None).Execute(context), sEnumValue: e.Id()))
        .ToList();
      var countEnabled = enableds.Count(e => e.allSuccess);
      if (countEnabled != 1)
        return null;
      return enableds.First(e => e.allSuccess).sEnumValue;
    }

    public override List<PolicyValueItemAction> ReportRegistrySettings(PolicyState policyState)
    {
      List<PolicyValueItemAction> actions;
      switch (policyState)
      {
        case PolicyState.Enabled:
          actions = EnumElement.Items.SelectMany(ei => ei.Values.GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.None)).ToList();
          break;
        case PolicyState.Disabled:
          actions = EnumElement.Items.SelectMany(ei => ei.Values.GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.DeleteValue)).ToList();
          break;
        case PolicyState.NotConfigured:
          actions = EnumElement.Items.SelectMany(ei => ei.Values.GetActions(PolicyValueAction.RemoveValue, PolicyValueDeleteType.None))
            .Concat(EnumElement.Items.SelectMany(ei => ei.Values.GetActions(PolicyValueAction.RemoveValue, PolicyValueDeleteType.DeleteValue)))
            .ToList();
          break;

        case PolicyState.Unknown:
        case PolicyState.Suspect:
        default:
          throw new ArgumentOutOfRangeException(nameof(policyState), policyState, null);
      }
      return actions;
    }
  }

  public class PolicyDecimalElementJob : PolicyElementJobBase
  {
    public DecimalElement DecimalElement => (DecimalElement)Element;
    public PolicyDecimalElementJob(DecimalElement policyElement) : base(policyElement)
    { }

    public override RegistryValueKind ValueKind => DecimalElement.StoreAsText ? RegistryValueKind.String : RegistryValueKind.DWord;
    public override Type ValueType => typeof(uint);
  }

  public class PolicyLongDecimalElementJob : PolicyElementJobBase
  {
    public LongDecimalElement LongDecimalElement => (LongDecimalElement)Element;
    public PolicyLongDecimalElementJob(LongDecimalElement policyElement) : base(policyElement)
    {
    }

    public override RegistryValueKind ValueKind => LongDecimalElement.StoreAsText ? RegistryValueKind.String : RegistryValueKind.QWord;
    public override Type ValueType => typeof(ulong);
  }

  public class PolicyBooleanElementJob : PolicyElementJobBase
  {
    public BooleanElement BooleanElement => (BooleanElement)Element;
    public PolicyBooleanElementJob(BooleanElement policyElement) : base(policyElement)
    {
    }

    public override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    public override Type ValueType => typeof(bool);

    //Multiple Use cases:
    //266 - TrueValues 1:DecimalValue FalseValues 1:DecimalValue
    // 46 - TrueValues 0: FalseValues 0: e.g. devinst.DriverSearchPlaces User
    //  2 - TrueValues 1:StringValue FalseValues 1:StringValue
    //  3 - TrueValues 1:DecimalValue FalseValues 1:DeleteValue
    //  3 - TrueValues 1:DecimalValue FalseValues 0:

    public override bool Enable(GpoContext context, object value)
    {
      //the data for the BooleanElement is bool
      if (value is not bool boolValue)
        throw new InvalidOperationException("Value is not a boolean");

      var actions = boolValue
        ? GetTrueValueItems().GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.None)
        : GetFalseValueItems(false).GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.None);

      return actions.Execute(context);
    }

    private IEnumerable<ValueItem> GetFalseValueItems(bool forGetState)
    {
      if (BooleanElement.FalseValues.Any())
        return BooleanElement.FalseValues;
      //if no FalseValues are defined, create the default value
      //return Enumerable.Repeat(new ValueItem(RegKey, RegValueName, new DecimalValue(0)), 1);
      //the default false value is deleting the value (not existing -> false) but the other option is also valid
      if (forGetState)
      {
        return
        [
          new ValueItem(RegKey, RegValueName, new DecimalValue(0)),
          new ValueItem(RegKey, RegValueName, new DeleteValue())
        ];
      }
      else
      {
        //when writing, only use one of the two options, not both
        return
        [
#warning check if the field required will do difference here
          //new ValueItem(RegKey, RegValueName, new DecimalValue(0)),
          new ValueItem(RegKey, RegValueName, new DeleteValue())
        ];

      }
    }

    private IEnumerable<ValueItem> GetTrueValueItems()
    {
      if (BooleanElement.TrueValues.Any())
        return BooleanElement.TrueValues;
      //if no TrueValues are defined, create the default value
      return Enumerable.Repeat(new ValueItem(RegKey, RegValueName, new DecimalValue(1)), 1);
    }

    private IEnumerable<ValueItem> GetAllValueItems(bool forGetState)
    {
      return GetFalseValueItems(forGetState).Concat(GetTrueValueItems());
    }

    public override bool Disable(GpoContext context)
    {
      //var actions = GetAllValueItems()
      //  .GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.DeleteValue)
      var actions = GetFalseValueItems(false)
        .GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.None)
        .ToList();

      return actions.Execute(context);
    }

    public override bool NotConfigured(GpoContext context)
    {
      var actions = GetAllValueItems(false)
        .GetActions(PolicyValueAction.RemoveValue, PolicyValueDeleteType.None)
        .Concat(GetAllValueItems(false).GetActions(PolicyValueAction.RemoveValue, PolicyValueDeleteType.DeleteValue))
        .ToList();

      return actions.Execute(context);
    }

    public override HashSet<PolicyState> GetState(GpoContext context)
    {
      bool enabled;


      using (context.Logger?.BeginScope($"Enabled"))
      {
        bool enabledFalse = GetFalseValueItems(true).GetActions(PolicyValueAction.ValueShouldExist, PolicyValueDeleteType.None).Execute(context, ValueItemsJobExtensions.ActionsCheckMode.Any);
        bool enabledTrue = GetTrueValueItems().GetActions(PolicyValueAction.ValueShouldExist, PolicyValueDeleteType.None).Execute(context, ValueItemsJobExtensions.ActionsCheckMode.Any);

        if (enabledFalse && enabledTrue)
          return new HashSet<PolicyState>() {PolicyState.Suspect};
        enabled = enabledFalse ^ enabledTrue;
      }

      bool disabled;
      using (context.Logger?.BeginScope($"Disabled"))
      {
        //disabled = GetAllValueItems()
        //  .GetActions(PolicyValueAction.ValueShouldExist, PolicyValueDeleteType.DeleteValue)
        disabled = GetFalseValueItems(true).GetActions(PolicyValueAction.ValueShouldExist, PolicyValueDeleteType.None).Execute(context, ValueItemsJobExtensions.ActionsCheckMode.Any);
      }

      bool notConfigured;
      using (context.Logger?.BeginScope($"NotConfigured"))
      {
        notConfigured = GetAllValueItems(false)
          .GetActions(PolicyValueAction.ValueShouldNotExist, PolicyValueDeleteType.None)
          .Concat(GetAllValueItems(false).GetActions(PolicyValueAction.ValueShouldNotExist, PolicyValueDeleteType.DeleteValue))
          .ToList()
          .Execute(context);
      }

      //Special use case here: Enable False and Disable have the same values -> report as Disabled
      if (enabled && disabled && !notConfigured)
        return new HashSet<PolicyState>() {PolicyState.Enabled, PolicyState.Disabled};

      return new HashSet<PolicyState>() { CalcPolicyState(enabled, disabled, notConfigured, context) };
    }

    public override object? GetValue(GpoContext context)
    {
      bool enabledFalse = GetFalseValueItems(true).GetActions(PolicyValueAction.ValueShouldExist, PolicyValueDeleteType.None).Execute(context, ValueItemsJobExtensions.ActionsCheckMode.Any);
      bool enabledTrue = GetTrueValueItems().GetActions(PolicyValueAction.ValueShouldExist, PolicyValueDeleteType.None).Execute(context, ValueItemsJobExtensions.ActionsCheckMode.Any);

      return enabledFalse ^ enabledTrue
        ? enabledTrue
        : null;
    }

    public override List<PolicyValueItemAction> ReportRegistrySettings(PolicyState policyState)
    {
      List<PolicyValueItemAction> actions = new List<PolicyValueItemAction>();
      switch (policyState)
      {
        case PolicyState.Enabled:
          actions.AddRange(GetTrueValueItems().GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.None));
          actions.AddRange(GetFalseValueItems(true).GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.None));
          break;
        case PolicyState.Disabled:
          actions.AddRange(GetFalseValueItems(true).GetActions(PolicyValueAction.SetValue, PolicyValueDeleteType.None));
          break;
        case PolicyState.NotConfigured:
          actions.AddRange(GetAllValueItems(false)
            .GetActions(PolicyValueAction.RemoveValue, PolicyValueDeleteType.None)
            .Concat(GetAllValueItems(false).GetActions(PolicyValueAction.RemoveValue, PolicyValueDeleteType.DeleteValue)));
          break;

        case PolicyState.Unknown:
        case PolicyState.Suspect:
        default:
          throw new ArgumentOutOfRangeException(nameof(policyState), policyState, null);
      }
      return actions;
    }
  }

  public class PolicyTextElementJob : PolicyElementJobBase
  {
    public TextElement TextElement => (TextElement)Element;
    public PolicyTextElementJob(TextElement policyElement) : base(policyElement)
    {
    }

    public override RegistryValueKind ValueKind => TextElement.Expandable ? RegistryValueKind.ExpandString : RegistryValueKind.String;
    public override Type ValueType => typeof(string);

    // <xs:attribute name="required" type="xs:boolean" default="false"/>
    // <xs:attribute name="maxLength" type="xs:unsignedInt" default="1023"/>
    // <xs:attribute name="expandable" type="xs:boolean" default="false"/>
    // <xs:attribute name="soft" type="xs:boolean" default="false"/>

    public override bool Enable(GpoContext context, object value)
    {
      //check value length
      if (value is string s && s.Length > TextElement.MaxLength)
        throw new InvalidOperationException($"Value is too long: {s.Length} Max:{TextElement.MaxLength}");

      return base.Enable(context, value);
    }
  }

  public class PolicyMultiTextElementJob : PolicyElementJobBase
  {
    public MultiTextElement MultiTextElement => (MultiTextElement)Element;
    public PolicyMultiTextElementJob(MultiTextElement policyElement) : base(policyElement)
    {
    }

    public override RegistryValueKind ValueKind => RegistryValueKind.MultiString;
    public override Type ValueType => typeof(string[]);

    // <xs:attribute name="required" type="xs:boolean" default="false"/>
    // <xs:attribute name="maxLength" type="xs:unsignedInt" default="1023"/>
    // <xs:attribute name="maxStrings" type="xs:unsignedInt" default="0"/>
    // <xs:attribute name="soft" type="xs:boolean" default="false"/>


    public override bool Enable(GpoContext context, object value)
    {
      //check value length
      if (value is string[] sArr)
      {
        if (MultiTextElement.MaxStrings > 0 && sArr.Length > MultiTextElement.MaxStrings)
          throw new InvalidOperationException($"Value has too many items: {sArr.Length} Max:{MultiTextElement.MaxStrings}");
        foreach (var (s, i) in sArr.Select((s, i) => (s, i)))
        {
          //not sure if this is correct, is MaxLength for the whole strings or per single line, MultiTextElement is missing in doc)
          if (s.Length > MultiTextElement.MaxLength)
            throw new InvalidOperationException($"Value [{i}] is too long: {s.Length} Max:{MultiTextElement.MaxLength}");
        }
      }

      return base.Enable(context, value);
    }
  }

  public class PolicyListElementJob : PolicyElementJobBase
  {
    public ListElement ListElement => (ListElement)Element;
    public PolicyListElementJob(ListElement policyElement) : base(policyElement)
    {
    }

    public override RegistryValueKind ValueKind => ListElement.Expandable ? RegistryValueKind.ExpandString : RegistryValueKind.String;
    public override Type ValueType => ListElement.ExplicitValue
      ? typeof(List<KeyValuePair<string, string>>)
      : typeof(List<string>);

    public override bool Enable(GpoContext context, object value)
    {
      //+ListElement.Expandable
      //ListElement.ExplicitValue
      //+ListElement.Additive
      //ListElement.ValuePrefix
      using (context.SwitchToLocalRegKey(RegKey, true))
      {
        if (!ListElement.Additive)
          context.SetValue(PolicyValueItemAction.DeleteValuesPrefix, " ", RegistryValueKind.String);

        var valueKind = ListElement.Expandable ? RegistryValueKind.ExpandString : RegistryValueKind.String;
        ICollection<KeyValuePair<string, string>> kvps;
        if (ListElement.ExplicitValue)
        {
          if (value is not ICollection<KeyValuePair<string, string>>)
            throw new InvalidOperationException($"Value has not expected type Current:{value?.GetType().Name ?? "<null>"} Expected: ICollection<KeyValuePair<string, string>> {Element.Id} of policy {Element.Parent.PrefixedName()}");
          kvps = (ICollection<KeyValuePair<string, string>>)value;
        }
        else
        {
          if (value is not ICollection<string> strings)
            throw new InvalidOperationException($"Value has not expected type Current:{value?.GetType().Name ?? "<null>"} Expected: ICollection<string> {Element.Id} of policy {Element.Parent.PrefixedName()}");

          if (ListElement.ValuePrefix != null) //also use Prefix if ValuePrefix="" !
            kvps = strings.Select((s, i) => new KeyValuePair<string, string>($"{ListElement.ValuePrefix}{i + 1}", s)).ToList(); //start with 1
          else
            //name and value are identical (see doc)
            kvps = strings.Select(s => new KeyValuePair<string, string>(s, s)).ToList();
        }
        foreach (var kvp in kvps)
        {
          context.SetValue(kvp.Key, kvp.Value, valueKind);
        }

        return true;
      }
    }

    public override bool Disable(GpoContext context)
    {
      using (context.SwitchToLocalRegKey(RegKey, true))
      {
        context.SetValue(PolicyValueItemAction.DeleteValuesPrefix, " ", RegistryValueKind.String);
        return true;
      }
    }

    public override bool NotConfigured(GpoContext context)
    {
      context.DeleteKey(ListElement.RegKey ?? ListElement.Parent.RegKey);
      return true;
    }

    public override HashSet<PolicyState> GetState(GpoContext context)
    {
      //getting raw values, is null if the key not exists
      var rawValues = GetValueRaw(context, out var invalidValueNames, out bool delValsExists,
        out var otherValuesExists);
      context.Logger?.LogTrace($"rawValues:{rawValues?.Count} invalidValueNames:{invalidValueNames?.Count} delValsExists:{delValsExists} otherValuesExists:{otherValuesExists}");
      if (invalidValueNames != null && invalidValueNames.Any())
        return new HashSet<PolicyState> {PolicyState.Suspect};

      if (rawValues == null || rawValues.Count == 0)
        return new HashSet<PolicyState> {PolicyState.NotConfigured};

      //Check Values

      if (ListElement.Additive && delValsExists && otherValuesExists)
        return new HashSet<PolicyState> {PolicyState.Suspect};

      return otherValuesExists
        ? new HashSet<PolicyState> {PolicyState.Enabled}
        : new HashSet<PolicyState> {PolicyState.Disabled};
    }

    private List<KeyValuePair<string, string>>? GetValueRaw(
      GpoContext context,
      out List<string>? invalidValueNames,
      out bool delValsExists,
      out bool otherValuesExists)
    {
      using (context.SwitchToLocalRegKey(RegKey, false))
      {
        if (!context.KeyExists)
        {
          invalidValueNames = null;
          delValsExists = false;
          otherValuesExists = false;
          return null;
        }
        var valueNames = context.GetValueNames();
        var values = valueNames
          .Select(valueName => (name: valueName, value: context.GetValue(valueName, out var valueKind), valueKind))
          .ToList();
        List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();
        invalidValueNames = new List<string>();
        foreach (var (name, value, valueKind) in values)
        {
          if (value is not string s || valueKind != RegistryValueKind.String)
            invalidValueNames.Add(name);
          else
            result.Add(new KeyValuePair<string, string>(name, s));
        }

        delValsExists = values.Any(e => string.Equals(e.name, PolicyValueItemAction.DeleteValuesPrefix, StringComparison.OrdinalIgnoreCase) && Equals(e.value, " "));
        otherValuesExists = delValsExists ? values.Count > 1 : values.Count > 0;

        return result;
      }
    }
    public override object? GetValue(GpoContext context)
    {
      var rawValues = GetValueRaw(context, out var invalidValueNames, out var delValsExists, out var otherValuesExists);
      if (rawValues == null)
        return null;
      if (invalidValueNames != null && invalidValueNames.Any())
        return null; //PolicyState.Suspect;

      if (ListElement.Additive && delValsExists && otherValuesExists)
        return null; //PolicyState.Suspect;

      rawValues = rawValues
        .Where(e => !string.Equals(e.Key, PolicyValueItemAction.DeleteValuesPrefix, StringComparison.OrdinalIgnoreCase))
        .ToList();
      return ListElement.ExplicitValue
        ? ListElement.ValuePrefix != null
          ? rawValues.Select(e => new KeyValuePair<string, string>(ToolBox.TrimPrefix(e.Key, ListElement.ValuePrefix), e.Value)).ToList()
          : rawValues
        : rawValues.Select(e => e.Value).ToList();
    }
  }

  public class PolicyValueItemAction
  {
    public const string DeleteValuePrefix = "**del.";
    public const string DeleteValuesPrefix = "**delvals.";
    public PolicyValueItemAction(PolicyValueAction action, string regKey, string regValueName, RegistryValueKind valueKind, object? value, PolicyValueDeleteType deleteType)
    {
      RawRegValueName = regValueName;
      RawValueKind = valueKind;
      switch (deleteType)
      {
        case PolicyValueDeleteType.None:
          break;
        case PolicyValueDeleteType.DeleteValue:
          regValueName = DeleteValuePrefix + regValueName;
          valueKind = RegistryValueKind.String;
          value = " ";
          break;
        case PolicyValueDeleteType.DeleteValues:
          regValueName = DeleteValuesPrefix;
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof(deleteType), deleteType, null);
      }

      Action = action;
      RegKey = regKey;
      RegValueName = regValueName;
      ValueKind = valueKind;
      PolicyValueDeleteType = deleteType;
      Value = value;
    }

    public PolicyValueAction Action { get; }
    public string RegKey { get; }
    public string RegValueName { get; }
    public string RawRegValueName { get; }
    public RegistryValueKind ValueKind { get; }
    public RegistryValueKind RawValueKind { get; }
    public PolicyValueDeleteType PolicyValueDeleteType { get; }
    public object? Value { get; }

    public bool Execute(GpoContext context)
    {
      if (context.Cache.TryGetValue(this, out var cachedResult))
      {
        context.Logger?.LogTrace($"{Action}: {RegKey}|{RegValueName} '{Value ?? "<null>"}' ({ValueKind}) - {cachedResult} (from cache)");
        return cachedResult;
      }

      using (context.SwitchToLocalRegKey(RegKey, Action is PolicyValueAction.SetValue or PolicyValueAction.RemoveValue or PolicyValueAction.RemoveValues))
      {
        bool result;
        switch (Action)
        {
          case PolicyValueAction.ValueShouldExist:
            {
              if (!context.KeyExists)
              {
                result = false;
              }
              else
              {
                var value = context.GetValue(RegValueName, out var valueKind);
                var locResult = value != null;
                if (Value != null)
                  locResult = Equals(value, Value); //check for fixed Value
                if (ValueKind != RegistryValueKind.Unknown)
                  locResult = locResult && valueKind == ValueKind;
                result = locResult;
              }

              break;
            }
          case PolicyValueAction.SingleValueShouldExist:
            {
              if (!context.KeyExists)
              {
                result = false;
              }
              else
              {
                var value = context.GetValue(RegValueName, out var valueKind);

                var locResult = value != null;
                if (Value != null)
                  locResult = Equals(value, Value); //check for fixed Value
                var valueNames = context.GetValueNames();
                locResult = locResult && valueNames.Length == 1 && valueNames[0] == RegValueName;
                result = locResult;
              }
              break;
            }
          case PolicyValueAction.ValueShouldNotExist:
            {
              var value = context.GetValue(RegValueName, out _);
              result = value == null;
              break;
            }
          case PolicyValueAction.NoValueShouldExist:
            {
              if (!context.KeyExists)
                result = true;
              else
              {
                var valueNames = context.GetValueNames();
                result = valueNames.Length == 0;
                //NoValueShouldExist is not used consider to remove this action
                //#warning Check if it is already fail if the key exists but no values?
              }

              break;
            }
          case PolicyValueAction.AnyValueShouldExist:
            {
              if (!context.KeyExists)
                result = false;
              else
              {
                var valueNames = context.GetValueNames();
                result = valueNames.Length > 0;
              }

              break;
            }
          case PolicyValueAction.SetValue:
            {
              if (!context.KeyExists)
                throw new InvalidOperationException($"Action ({Action}), RegKey does not exist, {RegKey} - {RegValueName}");
              if (Value == null)
                throw new InvalidOperationException($"Action ({Action}), Value is null, {RegKey} - {RegValueName}");
              context.SetValue(RegValueName, Value ?? throw new InvalidOperationException($"Action ({Action}) does not have a value{RegKey} - {RegValueName}"), ValueKind);
              result = true;
              break;
            }
          case PolicyValueAction.RemoveValue:
            {
              if (context.KeyExists)
                context.DeleteValue(RegValueName);
              result = true;
              break;
            }
          case PolicyValueAction.RemoveValues:
            {
              if (context.KeyExists)
              {
                var valueNames = context.GetValueNames();
                foreach (var valueName in valueNames)
                {
                  context.DeleteValue(valueName);
                }
              }
#warning Check if we need to remove the key and even the empty parent keys (or if this is done automatically)?
              result = true;
              break;
            }
          default:
            throw new ArgumentOutOfRangeException();
        }
        context.Cache[this] = result;
        if (context.DryRun)
          context.Logger?.LogDebug($"{Action}: {RegKey}|{RegValueName} '{Value ?? "<null>"}' ({ValueKind}) - {result}");
        else
        {
          if (!(Action is PolicyValueAction.RemoveValue or PolicyValueAction.RemoveValues or PolicyValueAction.SetValue))
            context.Logger?.LogTrace($"{Action}: {RegKey}|{RegValueName} '{Value ?? "<null>"}' ({ValueKind}) - {result}");
          //else: the real writes are logged as Debug in the SetValue and DeleteValue methods
        }

        return result;
      }
    }
  }
  public class PolicyValueItemActionComparer : IEqualityComparer<PolicyValueItemAction>
  {
    private static PolicyValueItemActionComparer? instance;
    public bool Equals(PolicyValueItemAction? x, PolicyValueItemAction? y)
    {
      if (x == null && y == null)
        return true;
      if (x == null || y == null)
        return false;
      return x.Action == y.Action && x.RegKey == y.RegKey && x.RegValueName == y.RegValueName && x.ValueKind == y.ValueKind && Equals(x.Value, y.Value);
    }

    public int GetHashCode(PolicyValueItemAction obj)
    {
      return HashCode.Combine(obj.Action, obj.RegKey, obj.RegValueName, obj.ValueKind, obj.Value);
    }

    public static PolicyValueItemActionComparer Instance
    {
      get
      {
        return instance ??= new PolicyValueItemActionComparer();
      }
    }
  }

  public class PolicyValueItemActionComparerJustRegKeyAndRegValueName : IEqualityComparer<PolicyValueItemAction>
  {
    private static PolicyValueItemActionComparerJustRegKeyAndRegValueName? instance;
    public bool Equals(PolicyValueItemAction? x, PolicyValueItemAction? y)
    {
      if (x == null && y == null)
        return true;
      if (x == null || y == null)
        return false;
      return x.RegKey == y.RegKey && x.RegValueName == y.RegValueName;
    }

    public int GetHashCode(PolicyValueItemAction obj)
    {
      return HashCode.Combine(obj.RegKey, obj.RegValueName);
    }

    public static PolicyValueItemActionComparerJustRegKeyAndRegValueName Instance
    {
      get
      {
        return instance ??= new PolicyValueItemActionComparerJustRegKeyAndRegValueName();
      }
    }
  }

  public class GpoContext : IDisposable
  {
    private RegistryKey? rootKey;
    private PolicyRegKey? policyRegKey;
    public IGroupPolicyObject Gpo { get; }
    public PolicyClass Class { get; }

    public GpoContext(IGroupPolicyObject gpo, PolicyClass @class)
    {
      if (@class == PolicyClass.Both)
        throw new InvalidOperationException("Class cannot be both");
      Gpo = gpo;
      Class = @class;
    }

    public RegistryKey RootKey => rootKey ??= Gpo.GetRootRegistryKey(Class == PolicyClass.Machine
      ? GpoSection.Machine
      : GpoSection.User);

    public IDisposable InitForPolicy(Policy policy)
    {
      if (policy == policyRegKey?.policy)
        return Disposable.Empty;
      policyRegKey?.Dispose();
      policyRegKey = new PolicyRegKey(RootKey, policy, false);
      return Disposable.Create(() =>
      {
        policyRegKey.Dispose();
        policyRegKey = null;
      });
    }

    private PolicyRegKey PolicyRegKey => policyRegKey ?? throw new InvalidOperationException("PolicyRegKey not defined for policy");

    public bool DryRun { get; set; } = false;
    public ILogger? Logger { get; set; } = null;

    public Dictionary<PolicyValueItemAction, bool> Cache { get; } = new Dictionary<PolicyValueItemAction, bool>(PolicyValueItemActionComparer.Instance);

    public void Save()
    {
      if (!DryRun)
      {
        Gpo.Save(Class == PolicyClass.Machine, true, GpoHelper.REGISTRY_EXTENSION_GUID, GpoHelper.CLSID_GPESnapIn);
      }
    }

    #region Registry funcs

    public IDisposable SwitchToLocalRegKey(string? localRegKey, bool writable)
    {
      if (policyRegKey == null)
        throw new InvalidOperationException("PolicyRegKey not defined for policy");
      return policyRegKey.SwitchToLocalRegKey(localRegKey, writable && !DryRun);
    }

    public bool KeyExists
    {
      get
      {
        if (policyRegKey == null)
          throw new InvalidOperationException("PolicyRegKey not defined for policy");
        return DryRun && policyRegKey.IsWritable || policyRegKey.RegKey != null;
      }
    }
    public string[] GetValueNames()
    {
      if (policyRegKey == null)
        throw new InvalidOperationException("PolicyRegKey not defined for policy");
      if (policyRegKey.RegKey == null)
        throw new InvalidOperationException("RegKey not defined");

      return policyRegKey.RegKey.GetValueNames();
    }

    public object? GetValue(string regValueName, out RegistryValueKind valueKind)
    {
      if (policyRegKey == null)
        throw new InvalidOperationException("PolicyRegKey not defined for policy");
      var value = policyRegKey.RegKey?.GetValueSafeTyped(regValueName, options: RegistryValueOptions.DoNotExpandEnvironmentNames);
      valueKind = value != null ? policyRegKey.RegKey!.GetValueKind(regValueName) : RegistryValueKind.Unknown;
      return value;
    }

    public void SetValue(string regValueName, object value, RegistryValueKind valueKind)
    {
      if (policyRegKey == null)
        throw new InvalidOperationException("PolicyRegKey not defined for policy");
      if (policyRegKey.RegKey == null)
        throw new InvalidOperationException("RegKey not defined");
      if (!DryRun)
      {
        var oldValue = GetValue(regValueName, out var oldValueKind);
        string? sOldValue = oldValue != null
          ? $" (old:'{oldValue ?? "<null>"}' [{oldValueKind})"
          : null;
        policyRegKey.RegKey.SetValueSafeTyped(regValueName, value, valueKind);
        Logger?.LogDebug($"SetValue: {policyRegKey.SRegKey}|{regValueName} '{value ?? "<null>"}' [{valueKind}]{sOldValue}");
      }
    }

    public void DeleteValue(string regValueName)
    {
      if (policyRegKey == null)
        throw new InvalidOperationException("PolicyRegKey not defined for policy");
      if (policyRegKey.RegKey == null)
        throw new InvalidOperationException("RegKey not defined");
      if (!DryRun)
      {
        var oldValue = GetValue(regValueName, out var oldValueKind);
        string? sOldValue = oldValue != null
          ? $" (old:'{oldValue ?? "<null>"}' [{oldValueKind})"
          : null;
        policyRegKey.RegKey.DeleteValue(regValueName, false);
        Logger?.LogDebug($"DeleteValue: {policyRegKey.SRegKey}|{regValueName}{sOldValue}");
      }
    }

    //deletes the key (only last element of regKey)
    public bool DeleteKey(string regKey)
    {
      if (policyRegKey == null)
        throw new InvalidOperationException("PolicyRegKey not defined for policy");

      //check if there are subkeys (to not delete a key with subkeys - potentially unwanted)
      using (policyRegKey.SwitchToLocalRegKey(regKey, false))
      {
        if (!KeyExists)
          return false; //key does not exist, nothing to delete
        if (policyRegKey.RegKey?.GetSubKeyNames().Length > 0)
          throw new InvalidOperationException($"RegKey '{regKey}' has subkey we should not delete this");
      }

      var indexOfLastBackSpace = regKey.LastIndexOf('\\');
      if (indexOfLastBackSpace <= 0)
        throw new InvalidOperationException($"RegKey '{regKey}' is not a valid key to delete (not able to get parent key)");
      var regKeyParent = regKey.Substring(0, indexOfLastBackSpace);
      var subKeyName = regKey.Substring(indexOfLastBackSpace + 1);
      if (string.IsNullOrWhiteSpace(subKeyName))
        throw new InvalidOperationException($"RegKey '{regKey}' is not a valid key to delete (no subkey name)");
      using (policyRegKey.SwitchToLocalRegKey(regKeyParent, true && !DryRun))
      {
        if (KeyExists && !DryRun)
        {
          policyRegKey.RegKey!.DeleteSubKey(subKeyName, false);
          Logger?.LogDebug($"DeleteKey: {policyRegKey.SRegKey}|{subKeyName}");
        }
      }
      policyRegKey.ReInit(); //may be the key is deleted, so retry to get it
      return true;
    }
    #endregion

    public void Dispose()
    {
      rootKey?.Dispose();
      policyRegKey?.Dispose();
    }
  }

  public static class ValueItemsJobExtensions
  {
    public static List<ValueItem> GetEnabledList(this Policy policy)
    {
      var result = policy.EnabledList.ToList();
      if (policy.RegValueName != null)
      {
        
        //add a value with that name (this is the default value for very simple Items)
        //example: SimpleItem "windows.NoCDBurning"
        //example: Complex: even if there are multiple elements this int value is written to show that this policy is active: 
        var value = policy.EnabledValue ?? new DecimalValue(1); //add this default Value "bits.BITS_MaxBandwidth"

        //do not add it, if the value is also set by the elements! (if adding it will be written multiple times AND the logic og detection might also fail)
        var valueIsAlreadyUsedByElements = policy.EnabledValue == null && policy.Elements.OfType<PolicyElementBase>()
          .Any(e => string.Equals(policy.RegValueName, e.RegValueName, StringComparison.OrdinalIgnoreCase));
        if (!valueIsAlreadyUsedByElements)
          result.Insert(0, new ValueItem(policy.RegKey, policy.RegValueName, value));
      }
      else
      {
        if (policy.EnabledValue != null) //no value name, but value
          throw new InvalidOperationException($"No value name, but EnabledValue {policy.PrefixedName()}");
      }

      return result;
    }

    public static List<ValueItem> GetDisabledList(this Policy policy)
    {
      var result = policy.DisabledList.ToList();
      if (policy.RegValueName != null)
      {
        if (policy.DisabledValue != null)
        {
          //add a value with that name (only if there is a DisabledValue
          var value = policy.DisabledValue;
          result.Insert(0, new ValueItem(policy.RegKey, policy.RegValueName, value));
        }
      }
      else
      {
        if (policy.DisabledValue != null) //no value name, but value
          throw new InvalidOperationException($"No value name, but DisabledValue {policy.PrefixedName()}");
      }

      return result;
    }

    public static List<PolicyValueItemAction> GetActions(this IEnumerable<ValueItem> valueItems, PolicyValueAction action, PolicyValueDeleteType deleteType)
    {
      return valueItems.Select(vi => vi.GetAction(action, deleteType)).ToList();
    }

    public static PolicyValueItemAction GetAction(this ValueItem valueItem, PolicyValueAction action, PolicyValueDeleteType deleteType)
    {
      RegistryValueKind valueKind;
      object? fixedValue;
      switch (valueItem.Value)
      {
        case DecimalValue decimalValue:
          valueKind = RegistryValueKind.DWord;
          fixedValue = decimalValue.Value;
          break;
        case DeleteValue deleteValue:
          valueKind = RegistryValueKind.None; 
          fixedValue = null;
          deleteType = PolicyValueDeleteType.DeleteValue;
          break;
        case LongDecimalValue longDecimalValue:
          valueKind = RegistryValueKind.QWord;
          fixedValue = longDecimalValue.Value;
          break;
        case StringValue stringValue:
          valueKind = RegistryValueKind.String;
          fixedValue = stringValue.Value;
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }
      if (action is PolicyValueAction.AnyValueShouldExist or PolicyValueAction.ValueShouldNotExist or PolicyValueAction.RemoveValue or PolicyValueAction.RemoveValues or PolicyValueAction.NoValueShouldExist or PolicyValueAction.SingleValueShouldExist)
      {
        valueKind = RegistryValueKind.None;
        fixedValue = null;
      }

      var regValueName = valueItem.RegValueName;
      var regKey = valueItem.RegKey;
      return new PolicyValueItemAction(action, regKey, regValueName, valueKind, fixedValue, deleteType);
    }

    public enum ActionsCheckMode
    {
      /// <summary>
      /// All Actions need to be true
      /// </summary>
      All,

      /// <summary>
      /// at least One action needs to be true
      /// </summary>
      Any,

      /// <summary>
      /// Only one action is allowed to be true, all others must be false
      /// </summary>
      Single,

      /// <summary>
      /// No actions are allowed to be true, all must be false
      /// </summary>
      None
    }

    public static bool Execute(this ICollection<PolicyValueItemAction> actions, GpoContext context, ActionsCheckMode actionsCheckMode = ActionsCheckMode.All) //, bool? valueIfEmpty
    {
      if (!actions.Any())
        return false; //(valueIfEmpty ?? throw new InvalidOperationException("provide a default value for empty actionslist to execute"));
      actions = actions
        .Distinct(PolicyValueItemActionComparer.Instance)
        .ToList();

      switch (actionsCheckMode)
      {
        case ActionsCheckMode.All:
          return actions.All(a => a.Execute(context));
        case ActionsCheckMode.Any:
          return actions.Any(a => a.Execute(context));
        case ActionsCheckMode.Single:
          return actions.Count(a => a.Execute(context)) == 1;
        case ActionsCheckMode.None:
          return actions.All(a => !a.Execute(context));
        default:
          throw new ArgumentOutOfRangeException(nameof(actionsCheckMode), actionsCheckMode, null);
      }
      return actions.All(a => a.Execute(context));
    }

    public static PolicyElementJobBase GetJob(this PolicyElement policyElement)
    {
      switch (policyElement)
      {
        case ListElement listElement:
          return new PolicyListElementJob(listElement);
        case DecimalElement decimalElement:
          return new PolicyDecimalElementJob(decimalElement);
        case EnumElement enumElement:
          return new PolicyEnumElementJob(enumElement);
        case BooleanElement booleanElement:
          return new PolicyBooleanElementJob(booleanElement);
        case LongDecimalElement longDecimalElement:
          return new PolicyLongDecimalElementJob(longDecimalElement);
        case MultiTextElement multiTextElement:
          return new PolicyMultiTextElementJob(multiTextElement);
        case TextElement textElement:
          return new PolicyTextElementJob(textElement);
        default:
          throw new InvalidOperationException($"PolicyElement {policyElement.GetType().Name} {policyElement.Id} is not defined for a job");
      }
    }
  }

}
