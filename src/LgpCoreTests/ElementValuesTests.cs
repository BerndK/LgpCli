using LgpCore.AdmParser;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using LgpCore.Gpo;
using Infrastructure;

namespace LgpCoreTests
{
  public class ElementValuesTests
  {
    [Test]
    public void DefaultValuesTest()
    {
      var admFolder = AdmFolder.SystemDefault();
      var elemControlTypes = new List<(Type elemType, Type controlType)>();
      foreach (var policy in admFolder.AllPolicies.Values)
      {
        var defaults = policy.DefaultValues();
        Console.WriteLine(policy.PrefixedName());
        foreach (var (policyElement, value) in defaults)
        {
          Console.WriteLine($" {policyElement.Id}: {value} Control:{policyElement.PresentationControl()}");
          elemControlTypes.Add((policyElement.GetType(), policyElement.PresentationControl().GetType()));
        }
      }

      Console.WriteLine();
      Console.WriteLine("Element types:");
      foreach (var (elemType, controlType) in elemControlTypes.Distinct())
      {
        Console.WriteLine($"  {elemType.Name} -> {controlType.Name}");
      }
    }

    [Test]
    public void TestMarkup()
    {
      //var text = "This is a [Markup] test [[with brackets inside ]] [/] and [[more brackets outside]]";
      //var text = "This is a [Markup] some [White]nested[/] text inside  [/] and more text outside";
      //var text = "[Yellow] Count [/][[ [White]{value}[/] ]]"; //the RegEx-Version does not handle this correctly
      var text = "[[[Green] admin[/]]]";

      var tokens = ParseMarkup(text);
      foreach (var (isMarkup, token) in tokens)
      {
        Console.WriteLine($"{(isMarkup ? "Markup" : "Text")}: '{token}'");
      }

      var count = 100_000;
      var sw = Stopwatch.StartNew();
      for (int i = 0; i < count; i++)
      {
        tokens = ParseMarkup(text);
      }
      Console.WriteLine($"{count} ParseMarkup: {sw.ElapsedMilliseconds}ms");

      Console.WriteLine();
      tokens = ParseMarkupRegEx(text);
      foreach (var (isMarkup, token) in tokens)
      {
        Console.WriteLine($"{(isMarkup ? "Markup" : "Text")}: '{token}'");
      }
      
      sw.Restart();
      for (int i = 0; i < count; i++)
      {
        tokens = ParseMarkupRegEx(text);
      }
      Console.WriteLine($"{count} ParseMarkup RegEx: {sw.ElapsedMilliseconds}ms");

    }
    public static List<(bool isMarkup, string value)> ParseMarkup(string text)
    {
      var s = text.AsSpan();
      var result = new List<(bool isMarkup, string value)>();
      bool isMarkup = false;
      while (s.Length > 0)
      {
        var idx = s.IndexOfAny("[]");
        if (idx < 0)
        {
          result.Add((isMarkup, s.ToString()));
          return result;
        }
        //detect double brackets (and not triple brackets)
        if (s.Length > (idx + 1) && s[idx] == s[idx + 1] && !(isMarkup && s.Length > (idx + 2) && s[idx] == s[idx + 2]))
        {
          idx++; //skip one of the doubles
          result.Add((isMarkup, s.Slice(0, idx).ToString()));
        }
        else
        { //single bracket
          if (idx > 0)
            result.Add((isMarkup, s.Slice(0, idx).ToString()));
          if (s[idx] == '[' && !isMarkup)
            isMarkup = true;
          else if (s[idx] == ']' && isMarkup)
            isMarkup = false;
        }
        s = s.Slice(idx + 1);
      }

      return result;
    }


    //public static List<(bool isMarkup, string text)> ParseMarkup(string text)
    //{
    //  var s = text.AsSpan();
    //  var result = new List<(bool isMarkup, string text)>();
    //  bool isMarkup = false;
    //  while (s.Length > 0)
    //  {
    //    var idx = s.IndexOfAny("[]");
    //    if (idx < 0)
    //    {
    //      result.Add((isMarkup, s.ToString()));
    //      return result;
    //    }
    //    //detect double brackets
    //    if (s.Length > idx && s[idx] == s[idx + 1])
    //    {
    //      idx++; //skip one of the doubles
    //      result.Add((isMarkup, s.Slice(0, idx).ToString()));
    //      s = s.Slice(idx+1);
    //    }
    //    else
    //    { //single bracket
    //      result.Add((isMarkup, s.Slice(0, idx).ToString()));
    //      if (s[idx] == '[' && !isMarkup)
    //        isMarkup = true;
    //      else if (s[idx] == ']' && isMarkup)
    //        isMarkup = false;
    //      s = s.Slice(idx + 1);
    //    }
    //  }

    //  return result;
    //}

    public static List<(bool isMarkup, string text)> ParseMarkupRegEx(string text)
    {
      var match = RegExHelper.MarkupRegex().Match(text); //generated regex
      //var match = RegExHelper.MarkupRegex2.Match(text); //standard regex

      if (match.Success)
      {
        var savedColors = new Stack<(ConsoleColor fore, ConsoleColor back)>();
        var texts = match.Groups["Text"].Captures.OfType<Capture>().Select(c => (capture: c, isMarkup: false));
        var markups = match.Groups["Markup"].Captures.OfType<Capture>().Select(c => (capture: c, isMarkup: true));

        return texts.Concat(markups)
          .OrderBy(e => e.capture.Index)
          .Select(t => (t.isMarkup, t.capture.Value))
          .ToList();
      }
      else
      {
        return new List<(bool isMarkup, string text)>();
      }
    }

    [Test]
    public void SplitEscapedTest()
    {
      ToolBox.SplitEscaped("key=value", '=').Should().BeEquivalentTo(new List<string>() { "key", "value" });
      ToolBox.SplitEscaped("key==value", '=').Should().BeEquivalentTo(new List<string>() { "key=value" });
      ToolBox.SplitEscaped("key==", '=').Should().BeEquivalentTo(new List<string>() { "key=" });
      ToolBox.SplitEscaped("key=", '=').Should().BeEquivalentTo(new List<string>() { "key", "" });
      ToolBox.SplitEscaped("=", '=').Should().BeEquivalentTo(new List<string>() { "", "" });
      ToolBox.SplitEscaped("", '=').Should().BeEquivalentTo(new List<string>() { "" });
      ToolBox.SplitEscaped("ke==y=va==lue", '=').Should().BeEquivalentTo(new List<string>() {"ke=y", "va=lue"});
      ToolBox.SplitEscaped("key=value=other", '=', 2).Should().BeEquivalentTo(new List<string>() { "key", "value=other" });

    }

    [Test]
    public void EscapeSeparatorTest()
    {
      ToolBox.EscapeSeparator("k=ey", '=').Should().Be("k==ey");
      ToolBox.SplitEscaped("k==ey", '=').Should().BeEquivalentTo(new List<string>() { "k=ey" });
    }

  }

  public static partial class RegExHelper
  {
    //https://regex101.com/r/tChGAj/1  https://regex101.com/delete/pZw9S5LPY2FUMDGEif4AnJDcvNm8IRyRxhDo
    [GeneratedRegex(@"(\[(?<Markup>.*?)\]|(?<Text>([^\[]|\[\[|\]\])+))*", RegexOptions.Compiled)]
    public static partial Regex MarkupRegex();

    public static Regex MarkupRegex2 => markupRegex;
    private static Regex markupRegex = new Regex(@"(\[(?<Markup>.*?)\]|(?<Text>([^\[]|\[\[|\]\])+))*", RegexOptions.Compiled);
  }
}
