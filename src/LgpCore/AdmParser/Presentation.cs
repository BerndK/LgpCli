using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LgpCore.AdmParser;

namespace LgpCore.AdmParser
{
  public class Presentation
  {
    public Presentation(string id, List<PresentationControl> controls)
    {
      Id = id;
      Controls = controls;
    }

    public string Id { get; }
    public List<PresentationControl> Controls { get; }
  }

  public abstract class PresentationControl
  { }

  public abstract class PresentationControlWithRefId : PresentationControl
  {
    public PresentationControlWithRefId(string refId) : base()
    {
      RefId = refId;
    }

    public string RefId { get; }

  }

  public abstract class PresentationControlWithRefIdAndText : PresentationControlWithRefId
  {
    public PresentationControlWithRefIdAndText(string refId, string text) : base(refId)
    {
      Text = text;
    }

    public string Text { get; }
  }

  public class CheckBoxControl : PresentationControlWithRefIdAndText
  {
    public CheckBoxControl(string refId, string text, bool defaultChecked) : base(refId, text)
    {
      DefaultChecked = defaultChecked;
    }

    public bool DefaultChecked { get; }
  }

  public class ComboBoxControl : PresentationControlWithRefId
  {
    public ComboBoxControl(string refId, string label, string? @default, string[] suggestion, bool noSort) : base(refId)
    {
      Label = label;
      Default = @default;
      Suggestion = suggestion;
      NoSort = noSort;
    }

    public string Label { get; }
    public string? Default { get; }
    public string[] Suggestion { get; }
    public bool  NoSort { get; }


  }
  public class DecimalTextBoxControl : PresentationControlWithRefIdAndText
  {
    public DecimalTextBoxControl(string refId, string text, uint defaultValue, bool spin, uint spinStep) : base(refId, text)
    {
      DefaultValue = defaultValue;
      Spin = spin;
      SpinStep = spinStep;
    }

    public uint DefaultValue { get; }
    public bool Spin { get; }
    public uint SpinStep { get; }
  }

  public class DropdownListControl : PresentationControlWithRefIdAndText
  {
    public DropdownListControl(string refId, string text, bool noSort, uint defaultItem) : base(refId, text)
    {
      NoSort = noSort;
      DefaultItem = defaultItem;
    }

    public bool NoSort { get; }
    public uint DefaultItem { get; }
  }

  public class ListBoxControl : PresentationControlWithRefIdAndText
  {
    public ListBoxControl(string refId, string text) : base(refId, text)
    { }
  }

  public class LongDecimalTextBoxControl : PresentationControlWithRefIdAndText
  {
    public LongDecimalTextBoxControl(string refId, string text, ulong defaultValue, bool spin, uint spinStep) : base(refId, text)
    {
      DefaultValue = defaultValue;
      Spin = spin;
      SpinStep = spinStep;
    }

    //this is defined as uint in the xsd, but ulong seems to be more useful
    public ulong DefaultValue { get; }
    public bool Spin { get; }
    public uint SpinStep { get; }

  }
  public class MultiTextBoxControl : PresentationControlWithRefIdAndText
  {
    public MultiTextBoxControl(string refId, string text, bool showAsDialog, uint defaultHeight) : base(refId, text)
    {
      ShowAsDialog = showAsDialog;
      DefaultHeight = defaultHeight;
    }

    public bool ShowAsDialog { get; }
    public uint DefaultHeight { get; }
  }
  public class TextControl : PresentationControl
  {
    public TextControl(string text) : base()
    {
      Text = text;
    }

    public string Text { get; }
  }
  public class TextBoxControl : PresentationControlWithRefId
  {
    public TextBoxControl(string refId, string label, string? defaultValueLabel) : base(refId)
    {
      Label = label;
      DefaultValueLabel = defaultValueLabel;
    }

    public string Label { get; }
    public string? DefaultValueLabel { get; }
  }
}
