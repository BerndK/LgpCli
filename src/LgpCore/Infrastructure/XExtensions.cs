using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Infrastructure
{

  public static partial class XExtensions
  {
    public static void EnsureAttributeExists(this XElement element, XName name, object defaultValue)
    {
      if (element.Attribute(name) == null)
        element.SetAttributeValue(name, defaultValue);
    }

    public static TEnum EnumValue<TEnum>(this XAttribute attribute) where TEnum : struct
    {
      return Enum.Parse<TEnum>((string)attribute);
    }

    public static TEnum? EnumValueNullable<TEnum>(this XAttribute? attribute) where TEnum : struct
    {
      if (attribute == null)
        return null;
      return Enum.Parse<TEnum>((string)attribute);
    }

    public static XElement RequiredElement(this XElement element, XName name)
    {
      return element.Element(name) ??
              throw new InvalidDataException($"Element {element.Name} does not contain required element {name}");
    }

    public static XAttribute RequiredAttribute(this XElement element, XName name)
    {
      return element.Attribute(name) ??
              throw new InvalidDataException($"Element {element.Name} does not contain required attribute {name}");
    }

    public static ushort? UshortNullable(this XAttribute? attribute) =>
      attribute == null ? new ushort?() : new ushort?(XmlConvert.ToUInt16(attribute.Value));
    public static ushort Ushort(this XAttribute attribute) => XmlConvert.ToUInt16(attribute.Value);

    public static Version ToVersion(this XAttribute attribute) => ParseVersion((string)attribute);
    public static Version ToVersionNullable(this XAttribute? attribute) =>
      attribute == null ? new Version() : ParseVersion((string)attribute);

    public static Version ParseVersion(string s)
    {
      if (Version.TryParse(s, out var result))
        return result;
      if (int.TryParse(s, out int major))
        return new Version(major, 0);
      return Version.Parse(s);
    }

    public static XDocument ToXDocument<T>(this T data, Func<T, XElement> toXml) => new XDocument(toXml(data));

    public static XDocument ToXDocument(this byte[] bytes)
    {
      using (var ms = new MemoryStream(bytes))
      {
        return XDocument.Load(ms);
      }
    }

    public static void ToStream<T>(this T data, Func<T, XElement> toXml, Stream stream)
    {
      var doc = new XDocument(toXml(data));
      doc.Save(stream);
    }

    public static T FromStream<T>(Stream stream, Func<XElement?, T> fromXml)
    {
      XDocument doc = XDocument.Load(stream);
      return fromXml(doc.Root);
    }

    public static T FromXml<T>(string xml, Func<XElement?, T> fromXml)
    {
      XDocument doc = XDocument.Parse(xml);
      return fromXml(doc.Root);
    }

    public static byte[] ToXmlBytes<T>(this T data, Func<T, XElement> toXml)
    {
      using (var ms = new MemoryStream())
      {
        data.ToStream(toXml, ms);
        return ms.ToArray();
      }
    }

    public static T FromXmlBytes<T>(this byte[] data, Func<XElement?, T> fromXml)
    {
      using (var ms = new MemoryStream(data))
      {
        return FromStream(ms, fromXml);
      }
    }

    public static void ToFile<T>(this T data, Func<T, XElement> toXml, string filePath)
    {
      var doc = new XDocument(toXml(data));
      doc.Save(filePath);
    }

    public static T FromFile<T>(string filePath, Func<XElement?, T> fromXml)
    {
      XDocument doc = XDocument.Load(filePath);
      return fromXml(doc.Root);
    }

    public static byte[] ToBytes(this XDocument doc)
    {
      using (var ms = new MemoryStream())
      {
        doc.Save(ms);
        return ms.ToArray();
      }
    }

    #region NameSpaces

    public static IDictionary<string, string>? GetNamespaces(this XDocument doc)
    {
      //this also works, but does not include the implicit xml:http:/www.w3.org/2001/XMLSchema-instance
      //returns Dictionary<string, XNamespace>
      //return doc.Root?.Attributes().
      //  Where(a => a.IsNamespaceDeclaration).
      //  ToDictionary(
      //    a => a.Name.Namespace == XNamespace.None ? String.Empty : a.Name.LocalName,
      //    a => XNamespace.Get(a.Value));

      XPathNavigator navigator = doc.CreateNavigator();
      navigator.MoveToFollowing(XPathNodeType.Element);
      return navigator.GetNamespacesInScope(XmlNamespaceScope.All);
    }

    public static XmlNamespaceManager NamespaceManager(this XDocument doc, string defaultNamespacePrefix)
    {
      var namespaces = doc.GetNamespaces();

      return NamespaceManager(namespaces, defaultNamespacePrefix);
    }

    private static XmlNamespaceManager NamespaceManager(IDictionary<string, string>? namespaces, string defaultNamespacePrefix)
    {
      if (namespaces == null || namespaces.Count == 0)
        return new XmlNamespaceManager(new NameTable());

      if (string.IsNullOrEmpty(defaultNamespacePrefix))
        throw new ArgumentException("Provide a default namespace prefix");

      if (namespaces.TryGetValue(defaultNamespacePrefix, out var ns))
        throw new ArgumentException($"the default namespace prefix '{defaultNamespacePrefix}' is already used by {ns}");

      var namespaceManager = new XmlNamespaceManager(new NameTable());
      foreach (var kvp in namespaces)
      {
        var prefix = string.IsNullOrEmpty(kvp.Key)
          ? defaultNamespacePrefix
          : kvp.Key;
        namespaceManager.AddNamespace(prefix, kvp.Value);
        //setting the empty "" prefix to the default namespace does not work for XPath!
      }

      return namespaceManager;
    }

    public static XmlNamespaceManager NamespaceManager(this XElement element, string defaultNamespacePrefix)
    {
      if (element.Document == null)
        throw new InvalidOperationException("element has no document, cannot create namespacemanager");

      return element.Document.NamespaceManager(defaultNamespacePrefix);
    }

    public static XmlNamespaceManager NamespaceManager(this XmlReader reader, string defaultNamespacePrefix)
    {
      Dictionary<string, string> namespaces = new Dictionary<string, string>();

      //move to content
      if (reader.NodeType == XmlNodeType.None)
      {
        reader.MoveToContent();
      }

      if (reader.HasAttributes)
      {
        while (reader.MoveToNextAttribute())
        {
          var name = reader.Name;
          if (name.StartsWith("xmlns", StringComparison.OrdinalIgnoreCase))
          {
            var prefix = name.Substring("xmlns".Length).TrimStart(':');
            namespaces.Add(prefix, reader.Value);
          }
        }
        // Move the reader back to the element node.
        reader.MoveToElement();
      }

      return NamespaceManager(namespaces, defaultNamespacePrefix);
      //var nsmgr = new XmlNamespaceManager(reader.NameTable);
      //var ns = nsmgr.LookupNamespace("");
      //if (ns != null)
      //{
      //  if (string.IsNullOrEmpty(defaultNamespacePrefix))
      //    throw new ArgumentException("Provide a default namespace prefix");

      //  var existingNs = nsmgr.LookupNamespace(defaultNamespacePrefix);
      //  if (!string.IsNullOrEmpty(existingNs))
      //    throw new ArgumentException($"the default namespace prefix '{defaultNamespacePrefix}' is already used by {existingNs}");
      //  nsmgr.AddNamespace(defaultNamespacePrefix, ns);
      //}

      //return nsmgr;
    }

    [GeneratedRegex("(?<prefix>.+):(?<name>.+)", RegexOptions.Compiled)]
    private static partial Regex NamespaceRegex();

    public static XName XName(this XmlNamespaceManager nsmgr, string name)
    {
      var match = NamespaceRegex().Match(name);
      if (!match.Success)
        throw new ArgumentException($"'{name}' is not valid, should be in form 'prefix:name'");
      var prefix = match.Groups["prefix"].Value;
      var ns = nsmgr.LookupNamespace(prefix);
      if (ns == null)
        throw new ArgumentException(
          $"No namespace found for prefix '{prefix}', defined prefixes: '{string.Join(",", nsmgr.OfType<string>().Where(s => !string.IsNullOrEmpty(s)))}'");
      return System.Xml.Linq.XName.Get(match.Groups["name"].Value, ns);
    }

    #endregion

    #region StructureOfXml

    public class ElemInfo
    {
      public string? Name;
      public int Count;
      public int Level;
      public Dictionary<string, AttribInfo> Attributes = new Dictionary<string, AttribInfo>();
      public Dictionary<string, ElemInfo> Elements = new Dictionary<string, ElemInfo>();
    }

    public class AttribInfo
    {
      public string? Name;
      public int Count;
    }

    public static string MaxStructure(this XElement? rootElement, bool includeNamespaces = true)
    {
      return MaxStructure(Enumerable.Repeat(rootElement, 1));
    }
    public static string MaxStructure(this IEnumerable<XElement?> rootElements, bool includeNamespaces = true, bool ignoreDefaultNamespace = true)
    {
      ElemInfo rootElemInfo = new ElemInfo() { Name = "ROOTS" };

      void HandleItem(XElement? rootElement, ElemInfo rootsElemInfo)
      {
        if (rootElement == null)
          return;

        XmlNamespaceManager? nsmgr = includeNamespaces
          ? rootElement.NamespaceManager("def")
          : null;
        var defaultNs = rootElement.GetDefaultNamespace();

        string NameFromElement(XElement xElement)
        {
          return includeNamespaces && (!ignoreDefaultNamespace || xElement.Name.NamespaceName != defaultNs)
            ? $"{nsmgr!.LookupPrefix(xElement.Name.NamespaceName)}:{xElement.Name.LocalName}"
            : xElement.Name.LocalName;
        }

        string NameFromAttribute(XAttribute xAttribute1)
        {
          if (includeNamespaces)
          {
            if (!string.IsNullOrEmpty(xAttribute1.Name.NamespaceName))
              return $"{nsmgr!.LookupPrefix(xAttribute1.Name.NamespaceName)}:{xAttribute1.Name.LocalName}";
          }

          return xAttribute1.Name.LocalName;
        }

        void HandleElement(XElement element, ElemInfo elemInfo)
        {
          elemInfo.Count++;

          foreach (var xAttribute in element.Attributes())
          {
            var name = NameFromAttribute(xAttribute);
            AttribInfo? attribInfo;
            if (!elemInfo.Attributes.TryGetValue(name, out attribInfo))
            {
              attribInfo = new AttribInfo() { Name = name };
              elemInfo.Attributes[name] = attribInfo;
            }
            attribInfo.Count++;
          }

          if (!string.IsNullOrEmpty(element.Value))
          {
            var name = "$VALUE";
            AttribInfo? attribInfo;
            if (!elemInfo.Attributes.TryGetValue(name, out attribInfo))
            {
              attribInfo = new AttribInfo() { Name = name };
              elemInfo.Attributes[name] = attribInfo;
            }
            attribInfo.Count++;
          }

          foreach (var xElement in element.Elements())
          {
            var name = NameFromElement(xElement);

            ElemInfo? localElemInfo;
            if (!elemInfo.Elements.TryGetValue(name, out localElemInfo))
            {
              localElemInfo = new ElemInfo() { Name = name, Level = elemInfo.Level + 1 };
              elemInfo.Elements[name] = localElemInfo;
            }
            HandleElement(xElement, localElemInfo);
          }
        }

        var name = NameFromElement(rootElement!);
        ElemInfo? localElemInfo;
        if (!rootsElemInfo.Elements.TryGetValue(name, out localElemInfo))
        {
          localElemInfo = new ElemInfo() { Name = name, Level = rootsElemInfo.Level + 1 };
          rootsElemInfo.Elements[name] = localElemInfo;
        }
        HandleElement(rootElement!, localElemInfo);
      }

      foreach (var rootElement in rootElements)
      {
        HandleItem(rootElement, rootElemInfo);
      }

      string BuildString(ElemInfo elemInfo)
      {
        var indent = new string(' ', elemInfo.Level);
        var value = $"{indent}{elemInfo.Name} {elemInfo.Count}";
        if (elemInfo.Attributes.Any())
        {
          value += $" ({string.Join(", ", elemInfo.Attributes.Values.OrderBy(e => e.Name).Select(e => $"{e.Name} {e.Count}"))})";
        }

        if (elemInfo.Elements.Any())
        {
          value += $"\r\n{string.Join("\r\n", elemInfo.Elements.Values.OrderBy(e => e.Name).Select(BuildString))}";
        }

        return value;
      }

      return BuildString(rootElemInfo);
    }
  }
  #endregion

  public static class SafeXAttr
  {
    /// <summary>
    /// Helper to use Linq/Fluent and add Attribute only if value not null
    /// if value is null, the returned content will be null which is ignored - no attribute added
    /// </summary>
    /// <example>new XElement("SomeElement", e.Value, SafeXAttr.New("SomeAttribute", e.SomeAttribute))</example>
    public static XAttribute? New(XName name, object? value)
    {
      if (value == null)
        return null;
      if (value is string s && !string.IsNullOrEmpty(s))
        value = s.Replace("\0", ""); //XML doesn't like zero-chars!
      return new XAttribute(name, value);
    }
  }
}
