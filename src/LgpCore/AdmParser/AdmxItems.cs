using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LgpCore.AdmParser
{
  public class Category
  {
    public Category(string name, string displayName, string? explainText, string? parentCategoryRef, string[] seeAlso,
      string? keywords)
    {
      Name = name;
      DisplayName = displayName;
      ExplainText = explainText;
      ParentCategoryRef = parentCategoryRef;
      SeeAlso = seeAlso;
      Keywords = keywords;
    }

    public string Name { get; }
    public string DisplayName { get; }
    public string? ExplainText { get; }
    public string? ParentCategoryRef { get; }

    public string[] SeeAlso { get; }
    public string? Keywords { get; }
  }

  [DebuggerDisplay("{Prefix}:{Namespace}")]
  public class PolicyNamespace
  {
    public PolicyNamespace(string ns, string prefix)
    {
      Namespace = ns;
      Prefix = prefix;
    }

    public string Namespace { get; }
    public string Prefix { get; }
  }

  [DebuggerDisplay("{DebuggerDisplay()}")]

  public class Policy
  {
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
    public Policy(AdmContent content, string name, PolicyClass cls, string displayName, string? explainText, string? presentationRef, string regKey, string? regValueName, string? parentCategoryRef, string? supportedOnRef, ValueBase? enabledValue, ValueBase? disabledValue, List<ValueItem> enabledList, List<ValueItem> disabledList, List<PolicyElement> elements)
    {
      Content = content;
      Name = name;
      Class = cls;
      DisplayName = displayName;
      ExplainText = explainText;
      PresentationRef = presentationRef;
      RegKey = regKey;
      RegValueName = regValueName;
      ParentCategoryRef = parentCategoryRef;
      SupportedOnRef = supportedOnRef;
      EnabledValue = enabledValue;
      DisabledValue = disabledValue;
      EnabledList = enabledList;
      DisabledList = disabledList;
      Elements = elements;
    }

    public AdmContent Content { get; }
    public LgpCategory? Category { get; set; } 
    public string Name { get; set; }
    public PolicyClass Class { get; set; }
    public string DisplayName { get; }
    public string? ExplainText { get; }
    public string? PresentationRef { get; }
    public string RegKey { get; }
    public string? RegValueName { get; }

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

    public string? ParentCategoryRef { get; }
    //other fields of BaseDescriptiveGroup annotation, seeAlso, keywords are ignored, because not really used in real world

    //		<xs:group ref="pd:BaseDescriptiveGroup"/>
    //		<xs:element name="supportedOn" type="pd:SupportedOnReference"/>
    //		<xs:element name="enabledValue" type="pd:Value" minOccurs="0" maxOccurs="1"/>
    //		<xs:element name="disabledValue" type="pd:Value" minOccurs="0" maxOccurs="1"/>
    //		<xs:element name="enabledList" type="pd:ValueList" minOccurs="0" maxOccurs="1"/>
    //		<xs:element name="disabledList" type="pd:ValueList" minOccurs="0" maxOccurs="1"/>
    //		<xs:element name="elements" type="pd:PolicyElements" minOccurs="0" maxOccurs="1"/>
    public string? SupportedOnRef { get; }

    public ValueBase? EnabledValue { get; }
    public ValueBase? DisabledValue { get; }
    public List<ValueItem> EnabledList { get; }
    public List<ValueItem> DisabledList { get; }
    public List<PolicyElement> Elements{ get; }

    private string DebuggerDisplay() => $"{this.GetType().Name}: {this.PrefixedName()}";
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
  public abstract class ValueBase;

  public class DeleteValue : ValueBase
  {
    public DeleteValue()
    {
    }
  }

  public class DecimalValue : ValueBase
  {
    public DecimalValue(uint value)
    {
      Value = value;
    }

    public uint Value { get; }
  }

  public class LongDecimalValue : ValueBase
  {
    public LongDecimalValue(ulong value)
    {
      Value = value;
    }

    public ulong Value { get; }
  }

  public class StringValue : ValueBase
  {
    public StringValue(string value)
    {
      Value = value;
    }

    public string Value { get; }
  }

  //<xs:complexType name="ValueItem">
  //	<xs:sequence>
  //		<xs:element name="value" type="pd:Value"/>
  //	</xs:sequence>
  //	<xs:attribute name="key" type="pd:registryKey"/>
  //	<xs:attribute name="valueName" type="pd:registryValueName" use="required"/>
  //</xs:complexType>
  public class ValueItem
  {
    public ValueItem(string regKey, string regValueName, ValueBase value)
    {
      RegKey = regKey;
      RegValueName = regValueName;
      Value = value;
    }

    public string RegKey { get; }
    public string RegValueName { get; }
    public ValueBase Value { get; }

  }

  //<xs:complexType name="ValueList">
  //	<xs:sequence>
  //		<xs:element name="item" type="pd:ValueItem" minOccurs="1" maxOccurs="unbounded"/>
  //	</xs:sequence>
  //	<xs:attribute name="defaultKey" type="pd:registryKey"/>
  //</xs:complexType>
  //ValueList is handled in parser implicitly

  [DebuggerDisplay("{DebuggerDisplay()}")]
  public abstract class PolicyElement
  {
    protected PolicyElement(Policy parent, string id, string? regKey)
    {
      Parent = parent;
      Id = id;
      RegKey = regKey;
    }

    public Policy Parent { get; }

    public string Id { get; }
    public string? RegKey { get; }
    private string DebuggerDisplay() => $"{this.GetType().Name}: {this.Id}";
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

  //<!-- Policy specification elements -->
  //<xs:attributeGroup name="PolicyElementAttributeGroup">
  //	<xs:annotation>
  //		<xs:documentation>Attribute group that all policy elements must have.</xs:documentation>
  //	</xs:annotation>
  //	<xs:attribute name="id" type="xs:string" use="required"/>
  //	<xs:attribute name="clientExtension" type="pd:GUID"/>
  //	<xs:attribute name="key" type="pd:registryKey"/>
  //	<xs:attribute name="valueName" type="pd:registryValueName"/>
  //</xs:attributeGroup>
  public abstract class PolicyElementBase : PolicyElement
  {
    protected PolicyElementBase(Policy parent, string id, string? clientExtensionGuid, string? regKey, string? regValueName) : base(parent, id, regKey)
    {
      //Id = id;
      ClientExtensionGuid = clientExtensionGuid;
      //RegKey = regKey;
      RegValueName = regValueName;
    }

    //public string Id { get; }
    public string? ClientExtensionGuid { get; }
    //public string? RegKey { get; }
    public string? RegValueName { get; }
  }

  //<xs:complexType name="BooleanElement">
  //	<xs:sequence>
  //		<xs:element name="trueValue" type="pd:Value" minOccurs="0" maxOccurs="1"/>
  //		<xs:element name="falseValue" type="pd:Value" minOccurs="0" maxOccurs="1"/>
  //		<xs:element name="trueList" type="pd:ValueList" minOccurs="0" maxOccurs="1"/>
  //		<xs:element name="falseList" type="pd:ValueList" minOccurs="0" maxOccurs="1"/>
  //	</xs:sequence>
  //	<xs:attributeGroup ref="pd:PolicyElementAttributeGroup"/>
  //</xs:complexType>
  public class BooleanElement : PolicyElementBase
  {
    public BooleanElement(Policy parent, string id, string? clientExtensionGuid, string? regKey, string? regValueName, List<ValueItem> trueValues, List<ValueItem> falseValues)
      : base(parent, id, clientExtensionGuid, regKey, regValueName)
    {
      TrueValues = trueValues;
      FalseValues = falseValues;
    }

    public List<ValueItem> TrueValues { get; }
    public List<ValueItem> FalseValues { get; }
  }

  //<xs:complexType name="DecimalElement">
  //	<xs:attributeGroup ref="pd:PolicyElementAttributeGroup"/>
  //	<xs:attribute name="required" type="xs:boolean" default="false"/>
  //	<xs:attribute name="minValue" type="xs:unsignedInt" default="0"/>
  //	<xs:attribute name="maxValue" type="xs:unsignedInt" default="9999"/>
  //	<xs:attribute name="storeAsText" type="xs:boolean" default="false"/>
  //	<xs:attribute name="soft" type="xs:boolean" default="false"/>
  //</xs:complexType>
  public class DecimalElement : PolicyElementBase
  {
    public DecimalElement(Policy parent, string id, string? clientExtensionGuid, string? regKey, string? regValueName, bool required, uint minValue, uint maxValue, bool storeAsText, bool soft)
      : base(parent, id, clientExtensionGuid, regKey, regValueName)
    {
      Required = required;
      MinValue = minValue;
      MaxValue = maxValue;
      StoreAsText = storeAsText;
      Soft = soft;
    }

    public bool Required { get; }
    public uint MinValue { get; }
    public uint MaxValue { get; }
    public bool StoreAsText { get; }
    public bool Soft { get; }
  }

  //<xs:complexType name="TextElement">
  //	<xs:attributeGroup ref="pd:PolicyElementAttributeGroup"/>
  //	<xs:attribute name="required" type="xs:boolean" default="false"/>
  //	<xs:attribute name="maxLength" type="xs:unsignedInt" default="1023"/>
  //	<xs:attribute name="expandable" type="xs:boolean" default="false"/>
  //	<xs:attribute name="soft" type="xs:boolean" default="false"/>
  //</xs:complexType>
  public class TextElement : PolicyElementBase
  {
    public TextElement(Policy parent, string id, string? clientExtensionGuid, string? regKey, string? regValueName, bool required, uint maxLength, bool expandable, bool soft)
      : base(parent, id, clientExtensionGuid, regKey, regValueName)
    {
      Required = required;
      MaxLength = maxLength;
      Expandable = expandable;
      Soft = soft;
    }

    public bool Required { get; }
    public uint MaxLength { get; }
    public bool Expandable { get; }
    public bool Soft { get; }

  }

  public class EnumItem
  {
    public EnumItem(EnumElement parent, string displayName, List<ValueItem> values)
    {
      Parent = parent;
      DisplayName = displayName;
      Values = values;
    }

    public string DisplayName { get; }
    public List<ValueItem> Values { get; set; }
    public EnumElement Parent { get; set; }
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
  public class EnumElement : PolicyElementBase
  {
    public EnumElement(Policy parent, string id, string? clientExtensionGuid, string? regKey, string? regValueName, List<EnumItem> items, bool required) 
      : base(parent, id, clientExtensionGuid, regKey, regValueName)
    {
      Items = items;
      Required = required;
    }

    public List<EnumItem> Items { get; }
    public bool Required { get; }
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
  public class ListElement : PolicyElement
  {
    public ListElement(Policy parent, string id, string? clientExtensionGuid, string? regKey, string? valuePrefix, bool additive, bool expandable, bool explicitValue)
      : base(parent, id, regKey)
    {
      //Id = id;
      ClientExtensionGuid = clientExtensionGuid;
      //RegKey = regKey;
      ValuePrefix = valuePrefix;
      Additive = additive;
      Expandable = expandable;
      ExplicitValue = explicitValue;
    }

    //public string Id { get; }
    public string? ClientExtensionGuid { get; }
    //public string? RegKey { get; }
    public string? ValuePrefix { get; }
    public bool Additive { get; }
    public bool Expandable { get; }
    public bool ExplicitValue { get; }
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
  public class LongDecimalElement : PolicyElementBase
  {
    public LongDecimalElement(Policy parent, string id, string? clientExtensionGuid, string? regKey, string? regValueName, bool required, ulong minValue, ulong maxValue, bool storeAsText, bool soft)
      : base(parent, id, clientExtensionGuid, regKey, regValueName)
    {
      Required = required;
      MinValue = minValue;
      MaxValue = maxValue;
      StoreAsText = storeAsText;
      Soft = soft;
    }

    public bool Required { get; }
    public ulong MinValue { get; }
    public ulong MaxValue { get; }
    public bool StoreAsText { get; }
    public bool Soft { get; }

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
  public class MultiTextElement : PolicyElementBase
  {
    public MultiTextElement(Policy parent, string id, string? clientExtensionGuid, string? regKey, string? regValueName, bool required, uint maxLength, uint maxStrings, bool soft)
      : base(parent, id, clientExtensionGuid, regKey, regValueName)
    {
      Required = required;
      MaxLength = maxLength;
      MaxStrings = maxStrings;
      Soft = soft;
    }

    public bool Required { get; }
    public uint MaxLength { get; }
    public uint MaxStrings { get; }
    public bool Soft { get; }
  }

  public enum PolicyClass
  {
    Machine,
    User,
    Both,
  }

  //<xs:complexType name="SupportedOnTable">
  //	<xs:sequence>
  //		<xs:element name="products" type="pd:SupportedProducts" minOccurs="0" maxOccurs="1"/>
  //		<xs:element name="definitions" type="pd:SupportedOnDefinitions" minOccurs="0" maxOccurs="1"/>
  //	</xs:sequence>
 // </xs:complexType>
  public class SupportedOnTable
  {
    private Dictionary<string, SupportedOnDefinition>? definitionsByName;

    public SupportedOnTable(List<SupportedProduct> products, List<SupportedOnDefinition> definitions)
    {
      Products = products;
      Definitions = definitions;
    }

    public List<SupportedProduct> Products { get; }
    public List<SupportedOnDefinition> Definitions { get; }
    public Dictionary<string, SupportedOnDefinition> DefinitionsByName => definitionsByName ??= Definitions.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
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
  public class SupportedProduct
  {
    public SupportedProduct(AdmContent content, List<SupportedMajorVersion> majorVersions, string name, string displayName)
    {
      Content = content;
      MajorVersions = majorVersions;
      Name = name;
      DisplayName = displayName;
    }

    public AdmContent Content { get; }
    public List<SupportedMajorVersion> MajorVersions { get; }
    public string Name { get; set; }
    public string DisplayName { get; }
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
  public class SupportedMajorVersion
  {
    public SupportedMajorVersion(AdmContent content, List<SupportedMinorVersion> minorVersions, string name, string displayName, uint versionIndex)
    {
      Content = content;
      MinorVersions = minorVersions;
      Name = name;
      DisplayName = displayName;
      VersionIndex = versionIndex;
    }

    public AdmContent Content { get; }
    public List<SupportedMinorVersion> MinorVersions { get; }
    public string Name { get; set; }
    public string DisplayName { get; }
    public uint VersionIndex { get; }
  }

  //<xs:complexType name="SupportedMinorVersion">
  //	<xs:annotation>
  //		<xs:documentation>Single version of a component to facilitate simple ranking of versions</xs:documentation>
  //	</xs:annotation>
  //	<xs:attribute name="displayName" type="pd:stringReference" use="required"/>
  //	<xs:attribute name="name" type="pd:itemName" use="required"/>
  //	<xs:attribute name="versionIndex" type="xs:unsignedInt" use="required"/>
  //</xs:complexType>
  public class SupportedMinorVersion
  {
    public SupportedMinorVersion(AdmContent content, string name, string displayName, uint versionIndex)
    {
      Content = content;
      Name = name;
      DisplayName = displayName;
      VersionIndex = versionIndex;
    }

    public AdmContent Content { get; }
    public string Name { get; set; }
    public string DisplayName { get; }
    public uint VersionIndex { get; }

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
  public class SupportedOnDefinition
  {
    public SupportedOnDefinition(AdmContent content, SupportedCondition? condition, string name, string displayName)
    {
      Content = content;
      Condition = condition;
      Name = name;
      DisplayName = displayName;
    }

    public AdmContent Content { get; }
    public SupportedCondition? Condition { get; }
    public string Name { get; set; }
    public string DisplayName { get; }
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
  public class SupportedCondition
  {
    public SupportedCondition(List<SupportedOnRange> ranges, List<SupportedOnReference> references)
    {
      Ranges = ranges;
      References = references;
    }

    public List<SupportedOnRange> Ranges { get; }
    public List<SupportedOnReference> References { get; }
  }
  public class SupportedOrCondition : SupportedCondition
  {
    public SupportedOrCondition(List<SupportedOnRange> ranges, List<SupportedOnReference> references) : base(ranges, references)
    { }
  }
  public class SupportedAndCondition : SupportedCondition
  {
    public SupportedAndCondition(List<SupportedOnRange> ranges, List<SupportedOnReference> references) : base(ranges, references)
    { }
  }

  //<xs:complexType name="SupportedOnRange">
  //	<xs:annotation>
  //		<xs:documentation>Supported version range.</xs:documentation>
  //	</xs:annotation>
  //	<xs:attribute name="ref" type="pd:itemReference" use="required"/>
  //	<xs:attribute name="minVersionIndex" type="xs:unsignedInt" use="optional"/>
  //	<xs:attribute name="maxVersionIndex" type="xs:unsignedInt" use="optional"/>
  //</xs:complexType>
  public class SupportedOnRange
  {
    public SupportedOnRange(string @ref, uint? minVersionIndex, uint? maxVersionIndex)
    {
      Ref = @ref;
      MinVersionIndex = minVersionIndex;
      MaxVersionIndex = maxVersionIndex;
    }

    public string Ref { get; }
    public uint? MinVersionIndex { get; }
    public uint? MaxVersionIndex { get; }
  }

  //<xs:complexType name="SupportedOnReference">
  //	<xs:annotation>
  //		<xs:documentation>Reference to a supported product definition (single version or complex definition).</xs:documentation>
  //	</xs:annotation>
  //	<xs:attribute name="ref" type="pd:itemReference" use="required"/>
  //</xs:complexType>
  public class SupportedOnReference
  {
    public SupportedOnReference(string @ref)
    {
      Ref = @ref;
    }

    public string Ref { get; }
  }

}