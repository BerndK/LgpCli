using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Infrastructure;

namespace LgpCore.AdmParser
{
  //see https://github.com/rkttu/AdmxParser/tree/main/docs/references files for the XSD definitions
  public class AdmContent
  {
    public FileInfo AdmxFile { get; }

    public string Language
    {
      get => language;
      set
      {
        bool GetResourceFileInfo(string s)
        {
          ResourceFile = new FileInfo(Path.Combine(AdmxFile.Directory!.FullName, s,
            Path.ChangeExtension(AdmxFile.Name, ".adml")));
          this.language = value;
          return ResourceFile.Exists;
        }

        if (!GetResourceFileInfo(value) && !string.Equals(value, AdmFolder.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
          GetResourceFileInfo(AdmFolder.DefaultLanguage);
        strings = null;
        presentations = null;
      }
    }

    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    private Dictionary<string, string>? strings;
    private Dictionary<string, Presentation>? presentations;
    private Dictionary<string, Category>? categories;
    private Dictionary<string, PolicyNamespace>? usingNamespaces;
    private List<Policy>? policies;
    private PolicyNamespace? targetNamespace;
    private string language;

    public AdmContent(string admxFile, string language, AdmFolder? parent) : this(new FileInfo(admxFile), language, parent)
    { }
    
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public AdmContent(FileInfo admxFile, string language, AdmFolder? parent)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    {
      AdmxFile = admxFile;
      Language = language;
      Parent = parent;
    }

    public AdmFolder? Parent { get; set; }

    public FileInfo ResourceFile { get; private set; }

    public Dictionary<string, string> Strings
    {
      get => strings ?? throw new NullReferenceException();
      set => strings = value;
    }

    public Dictionary<string, Presentation> Presentations
    {
      get => presentations ?? throw new NullReferenceException();
      set => presentations = value;
    }

    public Dictionary<string, Category> Categories
    {
      get => categories ?? throw new NullReferenceException();
      set => categories = value;
    }

    public PolicyNamespace TargetNamespace
    {
      get => targetNamespace ?? throw new NullReferenceException();
      set => targetNamespace = value;
    }

    /// <summary>
    /// Using Namespaces (key is Prefix)
    /// </summary>
    public Dictionary<string, PolicyNamespace> UsingNamespaces
    {
      get => usingNamespaces ?? throw new NullReferenceException();
      set => usingNamespaces = value;
    }

    public List<Policy> Policies
    {
      get => policies ?? throw new NullReferenceException();
      set => policies = value;
    }

    public SupportedOnTable? SupportedOn { get; set; }

    public void Parse()
    {
      if (!AdmxFile.Exists)
        throw new InvalidOperationException($"{AdmxFile.FullName} does not exist");

      ParseContent();
      ParseResources();
    }

    public void ParseContent()
    {
      //Not sure if this is needed
      //#warning Implement a detection for the current used Windows Version, read the limitations and apply them to limit the policies!
      if (!AdmxFile.Exists)
        throw new InvalidOperationException($"{AdmxFile.FullName} does not exist");
      var doc = XDocument.Load(AdmxFile.FullName);
      if (doc.Root == null)
        return;
      var ns = doc.Root.GetDefaultNamespace();

      this.Categories = ParseCategories(doc.Root, ns)
        .ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
      this.ParseNamespaces(doc.Root, ns);
      this.Policies = ParsePolicies(doc.Root, ns);
      this.SupportedOn = ParseSupportedOnTable(doc.Root, ns);

      //policyDefinitions 215 ($VALUE 23, revision 215, schemaVersion 215, xmlns 208, xmlns:xsd 207, xmlns:xsi 208)
      // categories 189
      //  category 412 (displayName 412, explainText 88, name 412)
      //   parentCategory 403 (ref 403)
      // policies 213 ($VALUE 23)
      //  policy 3300 ($VALUE 97, class 3300, clientExtension 62, displayName 3300, explainText 3300, key 3300, name 3300, presentation 1530, valueName 2003)
      //   disabledList 146 ($VALUE 17)
      //    item 928 ($VALUE 45, key 928, valueName 928)
      //     value 928 ($VALUE 45)
      //      decimal 263 (value 263)
      //      delete 620
      //      string 45 ($VALUE 45)
      //   disabledValue 1589 ($VALUE 56)
      //    decimal 1533 (value 1533)
      //    string 56 ($VALUE 56)
      //   elements 1503 ($VALUE 24)
      //    boolean 320 ($VALUE 2, id 320, key 22, required 16, valueName 320)
      //     falseValue 271 ($VALUE 2)
      //      decimal 266 (value 266)
      //      delete 3
      //      string 2 ($VALUE 2)
      //     trueValue 274 ($VALUE 2)
      //      decimal 272 (value 272)
      //      string 2 ($VALUE 2)
      //    decimal 368 (clientExtension 21, id 368, key 29, maxvalue 1, maxValue 334, minValue 183, required 119, storeAsText 5, valueName 368)
      //    enum 951 ($VALUE 21, clientExtension 2, id 951, key 29, required 794, valueName 951)
      //     item 3495 ($VALUE 194, displayName 3495)
      //      value 3495 ($VALUE 194)
      //       decimal 3298 (value 3298)
      //       delete 3
      //       string 194 ($VALUE 194)
      //      valueList 63
      //       item 3148 (key 3148, valueName 3148)
      //        value 3148
      //         decimal 3134 (value 3134)
      //         delete 14
      //    list 117 (additive 85, clientExtension 5, explicitValue 43, id 117, key 107, valueName 3, valuePrefix 28)
      //    multiText 77 (id 77, maxLength 4, maxStrings 2, required 2, valueName 77)
      //    text 286 (clientExtension 1, expandable 15, id 286, key 11, maxLength 52, required 130, valueName 286)
      //   enabledList 134 ($VALUE 13)
      //    item 305 ($VALUE 39, key 305, valueName 305)
      //     value 305 ($VALUE 39)
      //      decimal 266 (value 266)
      //      string 39 ($VALUE 39)
      //   enabledValue 1601 ($VALUE 56)
      //    decimal 1545 (value 1545)
      //    string 56 ($VALUE 56)
      //   parentCategory 3300 (ref 3300)
      //   supportedOn 3300 (ref 3300)
      // policyNamespaces 215
      //  target 215 (namespace 215, prefix 215)
      //  using 238 (namespace 238, prefix 238)
      // resources 215 (fallbackCulture 1, minRequiredRevision 215)
      // supersededAdm 8 (fileName 8)
      // supportedOn 23
      //  definitions 22
      //   definition 187 (displayName 187, name 187)
      //    and 20
      //     range 18 (maxVersionIndex 3, minVersionIndex 17, ref 18)
      //     reference 21 (ref 21)
      //    or 139
      //     range 140 (maxVersionIndex 52, minVersionIndex 136, ref 140)
      //     reference 62 (minVersionIndex 1, ref 62)
      //  products 1
      //   product 7 (displayName 7, name 7)
      //    majorVersion 36 (displayName 36, name 36, versionIndex 36)
      //     minorVersion 26 (displayName 26, name 26, versionIndex 26)

    }

    internal List<Category> ParseCategories(XElement root, XNamespace ns)
    {
      //<xs:group name="BaseDescriptiveGroup">
      //	<xs:sequence>
      //		<xs:element name="annotation" type="pd:Annotation" minOccurs="0" maxOccurs="unbounded"/>
      //		<xs:element name="parentCategory" type="pd:CategoryReference" minOccurs="0" maxOccurs="1"/>
      //		<xs:element name="seeAlso" type="xs:string" minOccurs="0" maxOccurs="unbounded"/>
      //		<xs:element name="keywords" type="xs:string" minOccurs="0"/>
      //	</xs:sequence>
      //</xs:group>
      //<xs:attributeGroup name="BaseDescriptiveAttributeGroup">
      //	<xs:attribute name="displayName" type="pd:stringReference" use="required"/>
      //	<xs:attribute name="explainText" type="pd:stringReference"/>
      //</xs:attributeGroup>
      //<!--
      //       Category related types
      //   -->
      //<xs:complexType name="CategoryReference">
      //	<xs:annotation>
      //		<xs:documentation>A reference to an already defined category.</xs:documentation>
      //	</xs:annotation>
      //	<xs:attribute name="ref" type="pd:itemReference" use="required"/>
      //</xs:complexType>
      //<xs:complexType name="Category">
      //	<xs:annotation>
      //		<xs:documentation>A grouping of policy definitions.</xs:documentation>
      //	</xs:annotation>
      //	<xs:sequence>
      //		<xs:group ref="pd:BaseDescriptiveGroup"/>
      //	</xs:sequence>
      //	<xs:attribute name="name" type="pd:itemName" use="required"/>
      //	<xs:attributeGroup ref="pd:BaseDescriptiveAttributeGroup"/>
      //</xs:complexType>

      var categoriesElement = root.Element(ns.GetName("categories"));
      if (categoriesElement == null)
        return new List<Category>();
      return categoriesElement.Elements(ns.GetName("category"))
        .Select(c =>
          new Category(
            (string) c.RequiredAttribute("name"),
            (string) c.RequiredAttribute("displayName"),
            (string?) c.Attribute("explainText"),
            (string?) c.Element(ns.GetName("parentCategory"))?.Attribute("ref"),
            c.Elements(ns.GetName("seeAlso")).Select(e => e.Value).ToArray(),
            (string?) c.Element(ns.GetName("keywords"))?.Value)
        )
        .ToList();
    }

    internal void ParseNamespaces(XElement root, XNamespace ns)
    {
      var policyNamespacesElement = root.RequiredElement(ns.GetName("policyNamespaces"));
      this.TargetNamespace = ParseNameSpace(policyNamespacesElement.RequiredElement(ns.GetName("target")));
      this.UsingNamespaces = policyNamespacesElement.Elements(ns.GetName("using")).Select(ParseNameSpace)
        .ToDictionary(pns => pns.Prefix, StringComparer.OrdinalIgnoreCase);
    }

    internal PolicyNamespace ParseNameSpace(XElement namespaceElement)
    {
      return new PolicyNamespace(
        (string) namespaceElement.RequiredAttribute("namespace"),
        (string) namespaceElement.RequiredAttribute("prefix")
      );
    }

    internal List<Policy> ParsePolicies(XElement root, XNamespace ns)
    {
      var policiesElement = root.Element(ns.GetName("policies"));
      if (policiesElement == null)
        return new List<Policy>();

        //<xs:complexType name="PolicyDefinition">
        //	<xs:sequence>
        //		<xs:group ref="pd:BaseDescriptiveGroup"/>
        //		<xs:element name="supportedOn" type="pd:SupportedOnReference"/>
        //		<xs:element name="enabledValue" type="pd:Value" minOccurs="0" maxOccurs="1"/>
        //		<xs:element name="disabledValue" type="pd:Value" minOccurs="0" maxOccurs="1"/>
        //		<xs:element name="enabledList" type="pd:ValueList" minOccurs="0" maxOccurs="1"/>
        //		<xs:element name="disabledList" type="pd:ValueList" minOccurs="0" maxOccurs="1"/>
        //		<xs:element name="elements" type="pd:PolicyElements" minOccurs="0" maxOccurs="1"/>
        //	</xs:sequence>
        //	<xs:attribute name="name" type="pd:itemName" use="required"/>
        //	<xs:attribute name="class" type="pd:PolicyClass" use="required"/>
        //	<xs:attributeGroup ref="pd:BaseDescriptiveAttributeGroup"/>
        //	<xs:attribute name="presentation" type="pd:presentationReference"/>
        //	<xs:attribute name="key" type="pd:registryKey" use="required"/>
        //	<xs:attribute name="valueName" type="pd:registryValueName"/>
        //</xs:complexType>
      return policiesElement.Elements(ns.GetName("policy"))
        .Select(p =>
        {
          var name = (string) p.RequiredAttribute("name");
          var regKey = (string) p.RequiredAttribute("key");
          var regValueName = (string?) p.Attribute("valueName");
          var enabledValue = ParseValue(p.Element(ns.GetName("enabledValue"))?.Elements().Single(), ns);
          var disabledValue = ParseValue(p.Element(ns.GetName("disabledValue"))?.Elements().Single(), ns);
          
          var enabledList = ParseValueList(p.Element(ns.GetName("enabledList")), regKey, ns);
          var disabledList = ParseValueList(p.Element(ns.GetName("disabledList")), regKey, ns);

          var policy = new Policy(
            this,
            name: name,
            cls: Enum.Parse<PolicyClass>((string)p.RequiredAttribute("class")),
            displayName: (string)p.RequiredAttribute("displayName"),
            explainText: (string?)p.Attribute("explainText"),
            presentationRef: (string?)p.Attribute("presentation"),
            regKey: regKey,
            regValueName: regValueName,
            parentCategoryRef: (string ?)p.Element(ns.GetName("parentCategory"))?.RequiredAttribute("ref"),
            supportedOnRef: (string?)p.Element(ns.GetName("supportedOn"))?.RequiredAttribute("ref"),
            enabledValue,
            disabledValue,
            enabledList: enabledList,
            disabledList: disabledList,
            elements: new List<PolicyElement>()
            );

          var elements = p.Element(ns.GetName("elements"))?.Elements()
            .Select(e => ParsePolicyElement(policy, e, regKey, ns))
            .ToList();
          if (elements != null)
            policy.Elements.AddRange(elements);

          return policy;
        })
        .ToList();
    }

    //<xs:complexType name="ValueList">
    //	<xs:sequence>
    //		<xs:element name="item" type="pd:ValueItem" minOccurs="1" maxOccurs="unbounded"/>
    //	</xs:sequence>
    //	<xs:attribute name="defaultKey" type="pd:registryKey"/>
    //</xs:complexType>
    //ValueList is handled in parser implicitly
    internal List<ValueItem> ParseValueList(XElement? elem, string regKey, XNamespace ns)
    {
      if (elem == null)
        return new List<ValueItem>();
      var localRegKey = (string?) elem.Attribute("defaultKey") ?? regKey;
      return elem.Elements(ns.GetName("item"))
        .Select(e => ParseValueItem(e, localRegKey, ns))
        .ToList();
    }

    //<xs:complexType name="ValueItem">
    //	<xs:sequence>
    //		<xs:element name="value" type="pd:Value"/>
    //	</xs:sequence>
    //	<xs:attribute name="key" type="pd:registryKey"/>
    //	<xs:attribute name="valueName" type="pd:registryValueName" use="required"/>
    //</xs:complexType>
    internal ValueItem ParseValueItem(XElement elem, string defaultRegKey, XNamespace ns)
    {
      return new ValueItem(
        (string?) elem.Attribute("key") ?? defaultRegKey,
        (string) elem.RequiredAttribute("valueName"),
        ParseValue(elem.RequiredElement(ns.GetName("value")).Elements().Single(), ns)!
      );
    }

    //<xs:complexType name="Value">
    //	<xs:choice>
    //		<xs:element name="delete">
    //			<xs:complexType></xs:complexType>
    //		</xs:element>
    //		<xs:element name="decimal">
    //			<xs:complexType>
    //				<xs:attribute name="value" type="xs:unsignedInt" use="required"/>
    //			</xs:complexType>
    //		</xs:element>
    //		<xs:element name="longDecimal">
    //			<xs:complexType>
    //				<xs:attribute name="value" type="xs:unsignedLong" use="required"/>
    //			</xs:complexType>
    //		</xs:element>
    //		<xs:element name="string">
    //			<xs:simpleType>
    //				<xs:restriction base="xs:string">
    //					<xs:maxLength value="255"/>
    //				</xs:restriction>
    //			</xs:simpleType>
    //		</xs:element>
    //	</xs:choice>
    //</xs:complexType>
    internal ValueBase? ParseValue(XElement? elem, XNamespace ns)
    {
      if (elem == null)
        return null;
      switch (elem.Name.LocalName)
      {
        case "delete":
          return new DeleteValue();
        case "decimal":
          return new DecimalValue((uint)elem.RequiredAttribute("value"));
        case "longDecimal":
          return new LongDecimalValue((ulong)elem.RequiredAttribute("value"));
        case "string":
          return new StringValue(elem.Value);

        default:
          throw new InvalidOperationException($"Unexpected Value Element {elem.Name}");
      }
    }

    //<xs:complexType name="PolicyElements">
    //	<xs:choice minOccurs="1" maxOccurs="unbounded">
    //		<xs:element name="boolean" type="pd:BooleanElement"/>
    //		<xs:element name="decimal" type="pd:DecimalElement"/>
    //		<xs:element name="text" type="pd:TextElement"/>
    //		<xs:element name="enum" type="pd:EnumerationElement"/>
    //		<xs:element name="list" type="pd:ListElement"/>
    //		<xs:element name="longDecimal" type="pd:LongDecimalElement"/>
    //		<xs:element name="multiText" type="pd:multiTextElement"/>
    //	</xs:choice>
    //</xs:complexType>
    internal PolicyElement ParsePolicyElement(Policy parent, XElement elem, string defaultKey, XNamespace ns)
    {

      switch (elem.Name.LocalName)
      {
        case "boolean":
        //<xs:complexType name="BooleanElement">
        //	<xs:sequence>
        //		<xs:element name="trueValue" type="pd:Value" minOccurs="0" maxOccurs="1"/>
        //		<xs:element name="falseValue" type="pd:Value" minOccurs="0" maxOccurs="1"/>
        //		<xs:element name="trueList" type="pd:ValueList" minOccurs="0" maxOccurs="1"/>
        //		<xs:element name="falseList" type="pd:ValueList" minOccurs="0" maxOccurs="1"/>
        //	</xs:sequence>
        //	<xs:attributeGroup ref="pd:PolicyElementAttributeGroup"/>
        //</xs:complexType>
        {
          ParsePolicyElementsAttributeGroup(elem, defaultKey, out var id, out var clientExtensionGuid, out var regKey, out var regValueName);

          var trueValue = ParseValue(elem.Element(ns.GetName("trueValue"))?.Elements().Single(), ns);
          var falseValue = ParseValue(elem.Element(ns.GetName("falseValue"))?.Elements().Single(), ns);
          var trueValues = ParseValueList(elem.Element(ns.GetName("trueList")), regKey ?? defaultKey, ns);
          var falseValues = ParseValueList(elem.Element(ns.GetName("falseList")), regKey ?? defaultKey, ns);
          if (trueValue != null)
          {
            if (regValueName == null)
              throw new InvalidOperationException($"BooleanElement has not ValueName defined but a single item");
            trueValues.Insert(0, new ValueItem(regKey ?? defaultKey, regValueName, trueValue));
          }

          if (falseValue != null)
          {
            if (regValueName == null)
              throw new InvalidOperationException($"BooleanElement has not ValueName defined but a single item");
            falseValues.Insert(0, new ValueItem(regKey ?? defaultKey, regValueName, falseValue));
          }

          return new BooleanElement(
            parent,
            id,
            clientExtensionGuid,
            regKey,
            regValueName,
            trueValues,
            falseValues);
        }
        //<xs:complexType name="DecimalElement">
        //	<xs:attributeGroup ref="pd:PolicyElementAttributeGroup"/>
        //	<xs:attribute name="required" type="xs:boolean" default="false"/>
        //	<xs:attribute name="minValue" type="xs:unsignedInt" default="0"/>
        //	<xs:attribute name="maxValue" type="xs:unsignedInt" default="9999"/>
        //	<xs:attribute name="storeAsText" type="xs:boolean" default="false"/>
        //	<xs:attribute name="soft" type="xs:boolean" default="false"/>
        //</xs:complexType>
        case "decimal":
        {
          ParsePolicyElementsAttributeGroup(elem, defaultKey, out var id, out var clientExtensionGuid, out var regKey, out var regValueName);
          return new DecimalElement(
            parent,
            id,
            clientExtensionGuid,
            regKey,
            regValueName,
            (bool?) elem.Attribute("required") ?? false,
            (uint?) elem.Attribute("minValue") ?? 0,
            (uint?) elem.Attribute("maxValue") ?? 9999,
            (bool?) elem.Attribute("storeAsText") ?? false,
            (bool?) elem.Attribute("soft") ?? false
          );
        }
        //<xs:complexType name="TextElement">
        //	<xs:attributeGroup ref="pd:PolicyElementAttributeGroup"/>
        //	<xs:attribute name="required" type="xs:boolean" default="false"/>
        //	<xs:attribute name="maxLength" type="xs:unsignedInt" default="1023"/>
        //	<xs:attribute name="expandable" type="xs:boolean" default="false"/>
        //	<xs:attribute name="soft" type="xs:boolean" default="false"/>
        //</xs:complexType>
        case "text":
        {
          ParsePolicyElementsAttributeGroup(elem, defaultKey, out var id, out var clientExtensionGuid, out var regKey,
            out var regValueName);
          return new TextElement(
            parent,
            id,
            clientExtensionGuid,
            regKey,
            regValueName,
            (bool?) elem.Attribute("required") ?? false,
            (uint?) elem.Attribute("maxLength") ?? 1023,
            (bool?) elem.Attribute("expandable") ?? false,
            (bool?) elem.Attribute("soft") ?? false
          );
        }

        //<xs:complexType name="EnumerationElement">
        //	<xs:sequence>
        //		<xs:element name="item" minOccurs="0" maxOccurs="unbounded">
        //			<xs:complexType>
        //				<xs:sequence>
        //					<xs:element name="value" type="pd:Value"/>
        //					<xs:element name="valueList" type="pd:ValueList" minOccurs="0" maxOccurs="1"/>
        //				</xs:sequence>
        //				<xs:attribute name="displayName" type="pd:stringReference" use="required"/>
        //			</xs:complexType>
        //		</xs:element>
        //	</xs:sequence>
        //	<xs:attributeGroup ref="pd:PolicyElementAttributeGroup"/>
        //	<xs:attribute name="required" type="xs:boolean" default="false"/>
        //</xs:complexType>
        //Example:
        //<enum id="IZ_PartnameIntranetZoneLockdownTemplate" valueName="Locked-Down Intranet" required="true">
        //  <item displayName="$(string.IZ_ItemnameLow)">
        //    <value>
        //      <decimal value="1" />
        //    </value>
        //    <valueList>
        //      <item key="Software\Policies\Microsoft\Windows\CurrentVersion\Internet Settings\Lockdown_Zones\1" valueName="1001">
        //        <value>
        //          <decimal value="3" />
        //        </value>
        //      </item>
        //      <item key="Software\Policies\Microsoft\Windows\CurrentVersion\Internet Settings\Lockdown_Zones\1" valueName="1004">
        //        <value>
        //          <decimal value="3" />
        //        </value>
        //      </item>
        //      ...
        case "enum":
        {
          ParsePolicyElementsAttributeGroup(elem, defaultKey, out var id, out var clientExtensionGuid, out var regKey, out var regValueName);
          var enumElement = new EnumElement(
            parent,
            id,
            clientExtensionGuid,
            regKey,
            regValueName,
            new List<EnumItem>(),
            (bool?) elem.Attribute("required") ?? false);

          enumElement.Items.AddRange(elem.Elements(ns.GetName("item"))
            .Select(e => ParseEnumItem(e, enumElement, regKey ?? defaultKey, regValueName, ns)));
          return enumElement;
        }

        //<xs:complexType name="ListElement">
        //	<xs:annotation>
        //		<xs:documentation>Describes a list element in a policy.</xs:documentation>
        //	</xs:annotation>
        //	<xs:attribute name="id" type="xs:string" use="required"/>
        //	<xs:attribute name="clientExtension" type="pd:GUID"/>
        //	<xs:attribute name="key" type="pd:registryKey"/>
        //	<xs:attribute name="valuePrefix" type="xs:string"/>
        //	<xs:attribute name="additive" type="xs:boolean" default="false"/>
        //	<xs:attribute name="expandable" type="xs:boolean" default="false"/>
        //	<xs:attribute name="explicitValue" type="xs:boolean" default="false"/>
        //</xs:complexType>
        case "list":
        {  return new ListElement(
            parent,
            (string)elem.RequiredAttribute("id"),
            (string?)elem.Attribute("clientExtension"),
            (string?)elem.Attribute("key") ?? defaultKey,
            (string?)elem.Attribute("valuePrefix"),
            (bool?)elem.Attribute("additive") ?? false,
            (bool?)elem.Attribute("expandable") ?? false,
            (bool?)elem.Attribute("explicitValue") ?? false
            );
        }
        //<xs:complexType name="LongDecimalElement">
        //	<xs:annotation>
        //		<xs:documentation>Describes a QWORD number/decimal element in a policy</xs:documentation>
        //	</xs:annotation>
        //	<xs:attributeGroup ref="pd:PolicyElementAttributeGroup"/>
        //	<xs:attribute name="required" type="xs:boolean" default="false"/>
        //	<xs:attribute name="minValue" type="xs:unsignedLong" default="0"/>
        //	<xs:attribute name="maxValue" type="xs:unsignedLong" default="9999"/>
        //	<xs:attribute name="storeAsText" type="xs:boolean" default="false"/>
        //	<xs:attribute name="soft" type="xs:boolean" default="false"/>
        //</xs:complexType>
        case "longDecimal":
        {
          ParsePolicyElementsAttributeGroup(elem, defaultKey, out var id, out var clientExtensionGuid, out var regKey,
            out var regValueName);
          return new LongDecimalElement(
            parent,
            id,
            clientExtensionGuid,
            regKey,
            regValueName,
            (bool?) elem.Attribute("required") ?? false,
            (ulong?) elem.Attribute("minValue") ?? 0,
            (ulong?) elem.Attribute("maxValue") ?? 9999,
            (bool?) elem.Attribute("storeAsText") ?? false,
            (bool?) elem.Attribute("soft") ?? false
          );
        }
        //<xs:complexType name="multiTextElement">
        //	<xs:annotation>
        //		<xs:documentation>Describes a multi line text element in a policy</xs:documentation>
        //	</xs:annotation>
        //	<xs:attributeGroup ref="pd:PolicyElementAttributeGroup"/>
        //	<xs:attribute name="required" type="xs:boolean" default="false"/>
        //	<xs:attribute name="maxLength" type="xs:unsignedInt" default="1023"/>
        //	<xs:attribute name="maxStrings" type="xs:unsignedInt" default="0"/>
        //	<xs:attribute name="soft" type="xs:boolean" default="false"/>
        //</xs:complexType>
        case "multiText":
        {
          ParsePolicyElementsAttributeGroup(elem, defaultKey, out var id, out var clientExtensionGuid, out var regKey,
            out var regValueName);
          return new MultiTextElement(
            parent,
            id,
            clientExtensionGuid,
            regKey,
            regValueName,
            (bool?) elem.Attribute("required") ?? false,
            (uint?) elem.Attribute("maxLength") ?? 1023,
            (uint?) elem.Attribute("maxStrings") ?? 0,
            (bool?) elem.Attribute("soft") ?? false
          );
        }
        default:
          throw new InvalidOperationException($"Unexpected Policy Element {elem.Name}");
      }
    }

    //<xs:attributeGroup name="PolicyElementAttributeGroup">
    //	<xs:annotation>
    //		<xs:documentation>Attribute group that all policy elements must have.</xs:documentation>
    //	</xs:annotation>
    //	<xs:attribute name="id" type="xs:string" use="required"/>
    //	<xs:attribute name="clientExtension" type="pd:GUID"/>
    //	<xs:attribute name="key" type="pd:registryKey"/>
    //	<xs:attribute name="valueName" type="pd:registryValueName"/>
    //</xs:attributeGroup>
    internal void ParsePolicyElementsAttributeGroup(XElement elem, string defaultKey, out string id, out string? clientExtensionGuid, out string? regKey, out string? regValueName)
    {
      id = (string) elem.RequiredAttribute("id");
      clientExtensionGuid = (string?) elem.Attribute("clientExtension");
      regKey = (string?)elem.Attribute("key"); //  ?? defaultKey; do not use the defaultKey here to better reflect the original content, in case use parent.RegKey
      regValueName = (string?) elem.Attribute("valueName");
    }

    //		<xs:element name="item" minOccurs="0" maxOccurs="unbounded">
    //			<xs:complexType>
    //				<xs:sequence>
    //					<xs:element name="value" type="pd:Value"/>
    //					<xs:element name="valueList" type="pd:ValueList" minOccurs="0" maxOccurs="1"/>
    //				</xs:sequence>
    //				<xs:attribute name="displayName" type="pd:stringReference" use="required"/>
    //			</xs:complexType>
    //		</xs:element>
    internal EnumItem ParseEnumItem(XElement elem, EnumElement parent, string regKey, string? regValueName, XNamespace ns)
    {
      var value = ParseValue(elem.RequiredElement(ns.GetName("value")).Elements().Single(), ns) ?? throw new InvalidOperationException();
      var values = ParseValueList(elem.Element(ns.GetName("valueList")), regKey, ns);
      var valueItem = new ValueItem(
        regKey ?? throw new InvalidOperationException(),
        regValueName ?? throw new InvalidOperationException(),
        value);
      values.Insert(0, valueItem);

      return new EnumItem(
        parent,
        (string) elem.RequiredAttribute("displayName"),
        values);
    }

    internal SupportedOnTable? ParseSupportedOnTable(XElement root, XNamespace ns)
    {
      //policyDefinitions:
      //  <xs:element name="supportedOn" type="pd:SupportedOnTable" minOccurs="0" maxOccurs="1"/>

      //<xs:complexType name="SupportedOnTable">
      //	<xs:sequence>
      //		<xs:element name="products" type="pd:SupportedProducts" minOccurs="0" maxOccurs="1"/>
      //		<xs:element name="definitions" type="pd:SupportedOnDefinitions" minOccurs="0" maxOccurs="1"/>
      //	</xs:sequence>
      //</xs:complexType>

      var supportedOnElement = root.Element(ns.GetName("supportedOn"));
      if (supportedOnElement == null)
        return null;

      var productsElement = supportedOnElement.Element(ns.GetName("products"));
      var definitionsElement = supportedOnElement.Element(ns.GetName("definitions"));
      return new SupportedOnTable(
        productsElement?.Elements(ns.GetName("product")).Select(e => ParseSupportedOnProduct(e, ns)).ToList() ?? new List<SupportedProduct>(),
        definitionsElement?.Elements(ns.GetName("definition")).Select(e => ParseSupportedOnDefinition(e, ns)).ToList() ?? new List<SupportedOnDefinition>());
    }

    //<xs:complexType name="SupportedProduct">
    //	<xs:annotation>
    //		<xs:documentation>A potentially supported product that can be referenced by a policy as being supported on.</xs:documentation>
    //	</xs:annotation>
    //	<xs:sequence>
    //		<xs:element name="majorVersion" type="pd:SupportedMajorVersion" minOccurs="0" maxOccurs="unbounded"/>
    //	</xs:sequence>
    //	<xs:attribute name="name" type="pd:itemName" use="required"/>
    //	<xs:attribute name="displayName" type="pd:stringReference" use="required"/>
    //</xs:complexType>
    private SupportedProduct ParseSupportedOnProduct(XElement element, XNamespace ns)
    {
      return new SupportedProduct(
        this,
        element.Elements(ns.GetName("majorVersion")).Select(e=>ParseSupportedMajorVersion(e, ns)).ToList(),
        (string)element.RequiredAttribute("name"),
        (string)element.RequiredAttribute("displayName")
        );
    }

    //<xs:complexType name="SupportedMajorVersion">
    //	<xs:annotation>
    //		<xs:documentation>A major version of a product that can be referenced by a policy as being supported on.</xs:documentation>
    //	</xs:annotation>
    //	<xs:sequence>
    //		<xs:element name="minorVersion" type="pd:SupportedMinorVersion" minOccurs="0" maxOccurs="unbounded"/>
    //	</xs:sequence>
    //	<xs:attribute name="name" type="pd:itemName" use="required"/>
    //	<xs:attribute name="displayName" type="pd:stringReference" use="required"/>
    //	<xs:attribute name="versionIndex" type="xs:unsignedInt" use="required"/>
    //</xs:complexType>
    private SupportedMajorVersion ParseSupportedMajorVersion(XElement element, XNamespace ns)
    {
      return new SupportedMajorVersion(
        this,
        element.Elements(ns.GetName("minorVersion")).Select(e => ParseSupportedMinorVersion(e, ns)).ToList(),
        (string) element.RequiredAttribute("name"),
        (string) element.RequiredAttribute("displayName"),
        (uint) element.RequiredAttribute("versionIndex"));
    }

    private SupportedMinorVersion ParseSupportedMinorVersion(XElement element, XNamespace ns)
    {
      return new SupportedMinorVersion(
        this,
        (string)element.RequiredAttribute("name"),
        (string)element.RequiredAttribute("displayName"),
        (uint)element.RequiredAttribute("versionIndex"));
    }

    //<xs:complexType name="SupportedOnDefinition">
    //	<xs:annotation>
    //		<xs:documentation>
    //               Definition of complex supported product major and/or minor versions, etc.
    //               The DisplayName must be a linguistic representation of the complex supported-on definition.
    //           </xs:documentation>
    //	</xs:annotation>
    //	<xs:sequence>
    //		<!-- Can have zero members as conversion from ADMs don't contain detailed information on this -->
    //		<xs:choice minOccurs="0" maxOccurs="1">
    //			<xs:element name="or" type="pd:SupportedOrCondition"/>
    //			<xs:element name="and" type="pd:SupportedAndCondition"/>
    //		</xs:choice>
    //	</xs:sequence>
    //	<xs:attribute name="name" type="pd:itemName" use="required"/>
    //	<xs:attribute name="displayName" type="pd:stringReference" use="required"/>
    //</xs:complexType>
    private SupportedOnDefinition ParseSupportedOnDefinition(XElement element, XNamespace ns)
    {
      SupportedCondition? supportedCondition = null;
      var supportedConditionElement = element.Elements().FirstOrDefault();
      if (supportedConditionElement != null)
      {
        switch (supportedConditionElement.Name.LocalName)
        {
          case "or":
            supportedCondition = ParseSupportedOrCondition(supportedConditionElement, ns);
            break;
          case "and":
            supportedCondition = ParseSupportedAndCondition(supportedConditionElement, ns);
            break;
          default:
            throw new InvalidOperationException($"'or' or 'and' element expected, but found: {supportedConditionElement.Name.LocalName}");
        }
      }
      
      return new SupportedOnDefinition(
        this,
        supportedCondition,
        (string)element.RequiredAttribute("name"),
        (string)element.RequiredAttribute("displayName"));
    }

    //<xs:complexType name="SupportedOrCondition">
    //	<xs:annotation>
    //		<xs:documentation>A group of supported components where at least one must be true for the policy definition to be supported.</xs:documentation>
    //	</xs:annotation>
    //	<xs:sequence>
    //		<xs:choice minOccurs="1" maxOccurs="unbounded">
    //			<xs:element name="range" type="pd:SupportedOnRange"/>
    //			<xs:element name="reference" type="pd:SupportedOnReference"/>
    //		</xs:choice>
    //	</xs:sequence>
    //</xs:complexType>
    private SupportedOrCondition ParseSupportedOrCondition(XElement element, XNamespace ns)
    {
      return new SupportedOrCondition(
        element.Elements(ns.GetName("range")).Select(e => ParseSupportedOnRange(e, ns)).ToList(),
        element.Elements(ns.GetName("reference")).Select(e => ParseSupportedOnReference(e, ns)).ToList());
    }

    //<xs:complexType name="SupportedAndCondition">
    //	<xs:annotation>
    //		<xs:documentation>A group of supported components that must all be true for the policy definition to be supported.</xs:documentation>
    //	</xs:annotation>
    //	<xs:sequence>
    //		<xs:choice minOccurs="1" maxOccurs="unbounded">
    //			<xs:element name="range" type="pd:SupportedOnRange"/>
    //			<xs:element name="reference" type="pd:SupportedOnReference"/>
    //		</xs:choice>
    //	</xs:sequence>
    //</xs:complexType>
    private SupportedAndCondition ParseSupportedAndCondition(XElement element, XNamespace ns)
    {
      return new SupportedAndCondition(
        element.Elements(ns.GetName("range")).Select(e => ParseSupportedOnRange(e, ns)).ToList(),
        element.Elements(ns.GetName("reference")).Select(e => ParseSupportedOnReference(e, ns)).ToList());
    }
    
    //<xs:complexType name="SupportedOnRange">
    //	<xs:annotation>
    //		<xs:documentation>Supported version range.</xs:documentation>
    //	</xs:annotation>
    //	<xs:attribute name="ref" type="pd:itemReference" use="required"/>
    //	<xs:attribute name="minVersionIndex" type="xs:unsignedInt" use="optional"/>
    //	<xs:attribute name="maxVersionIndex" type="xs:unsignedInt" use="optional"/>
    //</xs:complexType>
    private SupportedOnRange ParseSupportedOnRange(XElement element, XNamespace ns)
    {
      return new SupportedOnRange(
        (string) element.RequiredAttribute("ref"),
        (uint?) element.Attribute("minVersionIndex"),
        (uint?) element.Attribute("maxVersionIndex"));
    }

    //<xs:complexType name="SupportedOnReference">
    //	<xs:annotation>
    //		<xs:documentation>Reference to a supported product definition (single version or complex definition).</xs:documentation>
    //	</xs:annotation>
    //	<xs:attribute name="ref" type="pd:itemReference" use="required"/>
    //</xs:complexType>
    private SupportedOnReference ParseSupportedOnReference(XElement element, XNamespace ns)
    {
      return new SupportedOnReference((string) element.RequiredAttribute("ref"));
    }

    public void ParseResources()
    {
      if (!ResourceFile.Exists)
        throw new InvalidOperationException($"{ResourceFile.FullName} does not exist");
      var doc = XDocument.Load(ResourceFile.FullName);
      if (doc.Root == null)
        return;
      //var nsmgr = doc.NamespaceManager("def");
      var ns = doc.Root.GetDefaultNamespace();

      this.DisplayName = doc.Root.Element(ns.GetName("displayName"))?.Value ?? string.Empty;
      this.Description = doc.Root.Element(ns.GetName("description"))?.Value ?? string.Empty;
      var resourcesElement = doc.Root.Element(ns.GetName("resources"));
      var presentationTableElement = resourcesElement?.Element(ns.GetName("presentationTable"));

      this.Presentations = presentationTableElement?.Elements(ns.GetName("presentation"))
        .Select(presentationElement =>
        {
          var id = ((string?) presentationElement.Attribute("id")) ?? throw new NullReferenceException("Attribute id is missing");
          var controls = presentationElement.Elements()
            .Select<XElement, PresentationControl>(controlElement =>
            {

              var refId = (string?) controlElement.Attribute("refId");
              var value = controlElement.Value;

              //[System.Xml.Serialization.XmlElementAttribute("checkBox", typeof(CheckBox), Order = 0)]
              //[System.Xml.Serialization.XmlElementAttribute("comboBox", typeof(ComboBox), Order = 0)]
              //[System.Xml.Serialization.XmlElementAttribute("decimalTextBox", typeof(AdmxParser.Serialization.DecimalTextBox), Order = 0)]
              //[System.Xml.Serialization.XmlElementAttribute("dropdownList", typeof(DropdownList), Order = 0)]
              //[System.Xml.Serialization.XmlElementAttribute("listBox", typeof(ListBox), Order = 0)]
              //[System.Xml.Serialization.XmlElementAttribute("longDecimalTextBox", typeof(LongDecimalTextBox), Order = 0)]
              //[System.Xml.Serialization.XmlElementAttribute("multiTextBox", typeof(MultiTextBox), Order = 0)]
              //[System.Xml.Serialization.XmlElementAttribute("text", typeof(string), Order = 0)]
              //[System.Xml.Serialization.XmlElementAttribute("textBox", typeof(TextBox), Order = 0)]
              switch (controlElement.Name.LocalName)
              {
                //<xs:complexType name="CheckBox">
                //	<xs:annotation>
                //		<xs:documentation>Represents a checkbox display element.</xs:documentation>
                //		<xs:documentation>Can be associated with a BooleanElement.</xs:documentation>
                //	</xs:annotation>
                //	<xs:simpleContent>
                //		<xs:extension base="pd:DataElementContent">
                //			<xs:attribute name="defaultChecked" type="xs:boolean" default="false"/>
                //		</xs:extension>
                //	</xs:simpleContent>
                //</xs:complexType>
                //<checkBox refId="<placeholderID>" defaultChecked="true|false">
                //  label
                //</checkBox>
                case "checkBox":
                  return new CheckBoxControl(
                    refId ?? throw new NullReferenceException("refId missing"),
                    value,
                    (bool?) controlElement.Attribute("defaultChecked") ?? false);
                
                //<xs:complexType name="ComboBox">
                //	<xs:annotation>
                //		<xs:documentation>Represents a combobox display element with default/suggested entries.</xs:documentation>
                //		<xs:documentation>Can be associated with a TextElement.</xs:documentation>
                //	</xs:annotation>
                //	<xs:complexContent>
                //		<xs:extension base="pd:DataElement">
                //			<xs:sequence>
                //				<xs:element name="label" type="xs:string"/>
                //				<xs:element name="default" type="xs:string" minOccurs="0" maxOccurs="1"/>
                //				<xs:element name="suggestion" type="xs:string" minOccurs="0" maxOccurs="unbounded"/>
                //			</xs:sequence>
                //			<xs:attribute name="noSort" type="xs:boolean" default="false"/>
                //		</xs:extension>
                //	</xs:complexContent>
                //</xs:complexType>
                //<comboBox refId="<placeholderID>" noSort ="true|false" >
                //  <label> … </label>
                //  <default> … </default>
                //  <suggestion> … </suggestion>
                //</comboBox>
                case "comboBox":
                  return new ComboBoxControl(
                    refId ?? throw new NullReferenceException("refId missing"), 
                    controlElement.RequiredElement(ns.GetName("label")).Value,
                    (string?)controlElement.Element(ns.GetName("default"))?.Value,
                    controlElement.Elements(ns.GetName("suggestion")).Select(e => e.Value).ToArray(),
                    (bool?)controlElement.Attribute("noSort") ?? false
                    );

                //<xs:complexType name="DecimalTextBox">
                //	<xs:annotation>
                //		<xs:documentation>Represents a text box with or without a spin control for entering decimal numbers.</xs:documentation>
                //		<xs:documentation>Can be associated with either a DecimalElement or TextElement.</xs:documentation>
                //	</xs:annotation>
                //	<xs:simpleContent>
                //		<xs:extension base="pd:DataElementContent">
                //			<xs:attribute name="defaultValue" type="xs:unsignedInt" default="1"/>
                //			<xs:attribute name="spin" type="xs:boolean" default="true"/>
                //			<xs:attribute name="spinStep" type="xs:unsignedInt" default="1"/>
                //		</xs:extension>
                //	</xs:simpleContent>
                //</xs:complexType>
                //<decimalTextBox refId="<placeholderID>"
                //   defaultValue="<placeholderNumericValue>"
                //   spin="true|false"
                //   spinStep="<placeholderNumericValue>">
                //     label
                //</decimalTextBox>
                case "decimalTextBox":
                  return new DecimalTextBoxControl(
                    refId ?? throw new NullReferenceException("refId missing"),
                    value,
                    (uint?)controlElement.Attribute("defaultValue") ?? 1,
                    (bool?)controlElement.Attribute("spin") ?? true,
                    (uint?)controlElement.Attribute("spinStep") ?? 1
                  );

                //<xs:complexType name="DropdownList">
                //	<xs:annotation>
                //		<xs:documentation>Represents a dropdown list display element.</xs:documentation>
                //		<xs:documentation>Can be associated with an EnumerationElement.</xs:documentation>
                //	</xs:annotation>
                //	<xs:simpleContent>
                //		<xs:extension base="pd:DataElementContent">
                //			<xs:attribute name="noSort" type="xs:boolean" default="false"/>
                //			<xs:attribute name="defaultItem" type="xs:unsignedInt"/>
                //		</xs:extension>
                //	</xs:simpleContent>
                //</xs:complexType>
                //<dropdownList refId="<placeholderID>"
                //   noSort="true|false"
                //   defaultItem ="<placeholderNumericValue>">
                //label
                //</dropdownList>
                case "dropdownList":
                  return new DropdownListControl(
                    refId ?? throw new NullReferenceException("refId missing"),
                    value,
                    (bool?)controlElement.Attribute("noSort") ?? false,
                    (uint?)controlElement.Attribute("defaultItem") ?? 0
                  );

                //<xs:complexType name="ListBox">
                //	<xs:annotation>
                //		<xs:documentation>Represents a listbox display element.</xs:documentation>
                //		<xs:documentation>Can be associated with a ListElement.</xs:documentation>
                //	</xs:annotation>
                //	<xs:simpleContent>
                //		<xs:extension base="pd:DataElementContent"></xs:extension>
                //	</xs:simpleContent>
                //</xs:complexType>
                //<listBox refId="<placeholderID>">Placeholder label:</listBox>
                case "listBox":
                  return new ListBoxControl(
                    refId ?? throw new NullReferenceException("refId missing"),
                    value);

                //<xs:complexType name="LongDecimalTextBox">
                //	<xs:annotation>
                //		<xs:documentation>Represents a text box with or without a spin control for entering 64-bit decimal numbers.</xs:documentation>
                //		<xs:documentation>Can be associated with either a LongDecimalElement or TextElement.</xs:documentation>
                //	</xs:annotation>
                //	<xs:simpleContent>
                //		<xs:extension base="pd:DataElementContent">
                //			<xs:attribute name="defaultValue" type="xs:unsignedInt" default="1"/>
                //			<xs:attribute name="spin" type="xs:boolean" default="true"/>
                //			<xs:attribute name="spinStep" type="xs:unsignedInt" default="1"/>
                //		</xs:extension>
                //	</xs:simpleContent>
                //</xs:complexType>
                case "longDecimalTextBox":
                  return new LongDecimalTextBoxControl(
                    refId ?? throw new NullReferenceException("refId missing"),
                    value,
                    (ulong?)controlElement.Attribute("defaultValue") ?? 1, //this is defined as uint in the xsd, but ulong seems to be more useful
                    (bool?)controlElement.Attribute("spin") ?? true,
                    (uint?)controlElement.Attribute("spinStep") ?? 1

                  );

                //<xs:complexType name="MultiTextBox">
                //	<xs:annotation>
                //		<xs:documentation>Represents a multi-line textbox display element.</xs:documentation>
                //		<xs:documentation>Can be associated with a multiTextElement.</xs:documentation>
                //	</xs:annotation>
                //	<xs:complexContent>
                //		<xs:extension base="pd:DataElement">
                //			<xs:attribute name="showAsDialog" type="xs:boolean" default="false"/>
                //			<xs:attribute name="defaultHeight" type="xs:unsignedInt" default="3"/>
                //		</xs:extension>
                //	</xs:complexContent>
                //</xs:complexType>
                case "multiTextBox":
                  return new MultiTextBoxControl(
                    refId ?? throw new NullReferenceException("refId missing"),
                    value, //this value is not defined in xsd, as this element is based on DataElement, not DataElementContent, but there are Values in the file so let's use them
                    (bool?) controlElement.Attribute("showAsDialog") ?? false,
                    (uint?) controlElement.Attribute("defaultHeight") ?? 3);
                
                //<text>Placeholder string text</text>
                case "text":
                  return new TextControl(value);

                //<xs:complexType name="TextBox">
                //	<xs:annotation>
                //		<xs:documentation>Represents a textbox display element.</xs:documentation>
                //		<xs:documentation>Can be associated with a TextElement.</xs:documentation>
                //	</xs:annotation>
                //	<xs:complexContent>
                //		<xs:extension base="pd:DataElement">
                //			<xs:sequence>
                //				<xs:element name="label" type="xs:string"/>
                //				<xs:element name="defaultValue" type="xs:string" minOccurs="0" maxOccurs="1"/>
                //			</xs:sequence>
                //		</xs:extension>
                //	</xs:complexContent>
                //</xs:complexType>
                //<textBox refId="<placeholderID>">
                //  <label> … </label>
                //  <defaultValue> … </defaultValue>
                //</textBox>
                case "textBox":
                  return new TextBoxControl(
                    refId ?? throw new NullReferenceException("refId missing"),
                    controlElement.RequiredElement(ns.GetName("label")).Value,
                    controlElement.Element(ns.GetName("defaultValue"))?.Value);
                default:
                  throw new InvalidOperationException($"Unexpected Control: {controlElement.Name.LocalName}");
              }
            })
            .OfType<PresentationControl>()
            .ToList();
          return new Presentation(id, controls);
        })
        .ToDictionary(p => p.Id, p => p)
        ?? new Dictionary<string, Presentation>(); //if no presentation data

      var stringTableElement = resourcesElement?.Element(ns.GetName("stringTable"));
      this.strings = stringTableElement?.Elements(ns.GetName("string"))
        .ToDictionary(e => (string)e.Attribute("id")!, e => e.Value)
         ?? new Dictionary<string, string>();

      //ROOTS 0
      // policyDefinitionResources 215 ($VALUE 215, revision 215, schemaVersion 215, xmlns 207, xmlns:xsd 206, xmlns:xsi 207)
      //  description 215 ($VALUE 212)
      //  displayName 215 ($VALUE 213)
      //  resources 215 ($VALUE 215)
      //   presentationTable 135 ($VALUE 134)
      //    presentation 1095 ($VALUE 1067, id 1095)
      //     checkBox 324 ($VALUE 324, defaultChecked 93, defaultItem 1, noSort 12, refId 324)
      //     decimalTextBox 375 ($VALUE 363, defaultvalue 4, defaultValue 318, refId 375, spin 5, spinStep 100, xml:space 15)
      //     dropdownList 516 ($VALUE 503, defaultItem 467, nosort 1, noSort 424, oSort 1, refId 516, xml:space 30)
      //     listBox 98 ($VALUE 97, refId 98, required 2)
      //     multiTextBox 80 ($VALUE 5, refId 80)
      //     text 942 ($VALUE 820)
      //     textBox 292 ($VALUE 292, refId 292)
      //      defaultValue 57 ($VALUE 42)
      //      label 292 ($VALUE 292, xml:space 11)
      //   stringTable 215 ($VALUE 215)
      //    string 7024 ($VALUE 7022, id 7024)
    }

    //lookup string like '$(string.ShutdownOptions)'
    public string GetLocatedString(string displayName)
    {
      //if (displayName.StartsWith("$(string."))
      //{
      //  string id = displayName.AsSpan(9, displayName.Length - 10).ToString();
      //  if (Strings.TryGetValue(id, out var result))
      //    return result;
      //}

      //return displayName;

      return displayName.StartsWith("$(string.")
        ? Strings.GetValueOrDefault(displayName[9..^1], displayName)
        : displayName;

    }
    public string? GetLocatedStringNullable(string? displayName)
    {
      return displayName != null
        ? GetLocatedString(displayName)
        : null;
    }

    public Presentation GetPresentation(string presentationRef)
    {
      return presentationRef.StartsWith("$(presentation.")
        ? Presentations.GetValueOrDefault(presentationRef[15..^1]) ?? throw new InvalidOperationException($"Presentation {presentationRef} not found")
        : throw new InvalidOperationException($"Unexpected presentation reference {presentationRef}");

    }

    //'$(string.ShutdownOptions)' -> 'ShutdownOptions'
    public static string DisplayStringId(string displayName)
    {
      return displayName.StartsWith("$(string.")
        ? displayName[9..^1]
        : displayName;
    }
  }

}
