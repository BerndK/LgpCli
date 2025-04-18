using Infrastructure;
using LgpCore.AdmParser;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentAssertions;
using LgpCore.Gpo;

namespace LgpCoreTests
{
  public class SearchTests
  {
    [Test]
    public async Task SearchTest()
    {
      var admFolder = AdmFolder.SystemDefault();
      await admFolder.ParseAsync();


      string StringSelector(Policy policy)
      {
        return policy.DisplayNameResolved() + policy.ExplainTextResolved();
      }

      var sw = Stopwatch.StartNew();
      var found = admFolder.AllPolicies.Values.SearchItemsOldStyle("Security|Internet Explorer", StringSelector, "|", StringComparison.OrdinalIgnoreCase).ToList();

      Console.WriteLine($"{found.Count} items found {sw.ElapsedMilliseconds}ms");

      sw.Restart();
      found = admFolder.AllPolicies.Values.SearchItems("Security|Internet Explorer", StringSelector, "|", StringComparison.OrdinalIgnoreCase).ToList();
      Console.WriteLine($"{found.Count} items found {sw.ElapsedMilliseconds}ms");

    }

    [Test]
    public void EscapeTest()
    {
      var text = "Space Quote\"Tab:\t,Return\rNewLine\n.";
      Console.WriteLine(text);
      var escaped = Regex.Escape(text);
      Console.WriteLine(escaped);
      var unescaped = Regex.Unescape(escaped);
      Console.WriteLine(unescaped);
    }
  }
}
