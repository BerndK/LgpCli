using System.Collections;
using System.CommandLine;
using System.CommandLine.Parsing;
using FluentAssertions;
using LgpCore.AdmParser;
using LgpCore;
using LgpCore.Gpo;
using LgpCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LgpCoreTests
{
  public class CommandLineTests : ServicedTestBase
  {
    protected override void DefineServices(ServiceCollection serviceCollection)
    {
      base.DefineServices(serviceCollection);
      serviceCollection.AddSingleton<AdmFolder>(AdmFolder.SystemDefault());
    }

    [TestCaseSource(typeof(CommandLineTestCases))] 
    public void CmdTest(string args)
    {
      var commandLine = new CommandLine(null);
      commandLine.Build(ServiceProvider);
      commandLine.EnableCommand.SetHandler(HandleEnable);
      var parseResult = commandLine.Parser.Parse(args);
      Console.WriteLine($"'{args}'");
      Console.WriteLine();
      Console.WriteLine(parseResult.ToString());
      commandLine.Parser.Invoke(args);
    }

    private void HandleEnable(IServiceProvider arg1, string arg2, PolicyClass? arg3, List<(string, List<string>)> keyValues, CommandLine.GetStateMode arg5)
    {
      Console.WriteLine($"{keyValues.Count}");
      foreach (var (key, values) in keyValues)
      {
        Console.WriteLine($"Key: '{key}'");
        foreach (var value in values)
        {
          Console.WriteLine($" '{value}'");
        }
      }
    }

    [TestCaseSource(typeof(CommandLineTestCases))]
    public void BatchCmdInfoTest(string args)
    {
      var commandLine = new CommandLine(null);
      commandLine.Build(ServiceProvider);
      var info = BatchCmd.ParseCommandLine(commandLine.Parser, string.Join(' ', args), 0);
      
      Console.WriteLine($"Command:'{info.CommandName}' Policy:{info.PolicyPrefixedName} {info.PolicyClass}");
    }



    [Test]
    public void CommandlineParse2Test()
    {
      var c = TypedCommand.Create("test", null,
        CommandLine.KeyAndValueOption
      );

      var policyElement = new TextElement(null!, "elem_id", null, null, null, true, 100, false, false);

      void TestArg(string arg)
      {
        Console.Write($"Arg:'{arg}'");

        var args = new string[] {"test", "-k", "elem_id", "-v", arg};
        string? value = null;
        c.SetHandler((List<(string, List<string>)> keyValues) =>
        {
          var elemValues = keyValues.Find(e => e.Item1 == policyElement.Id);
          value = policyElement.ValueFromCommandLine(elemValues.Item2) as string;
        });

        c.Invoke(args);

        Console.WriteLine($"Arg:'{arg} -> Value:'{value}'");
      }

      TestArg("value");
      TestArg("va\"lue");
      TestArg("va\"\"lue");
      TestArg("va\\\"lue");
      TestArg("va\\\\lue");
      TestArg("va lue");
      TestArg("va\" lue");
      TestArg("va\"\" lue");
      TestArg("va\\\" lue");
      TestArg("va\\\\ lue");
      
    }

    [TestCase("value")]
    [TestCase("\"value\"")]
    [TestCase("va\"lue")]
    [TestCase("va\tlue")]
    [TestCase("")]
    [TestCase("value")]
    [TestCase("va\"lue")]
    [TestCase("va\"\"lue")]
    [TestCase("va\\\"lue")]
    [TestCase("va\\\\\"lue")]
    [TestCase("va\\lue")]
    [TestCase("va\\\\lue")]
    [TestCase("va lue")]
    [TestCase("va\" lue")]
    [TestCase("va\"\" lue")]
    [TestCase("va\\\\\" lue")]
    [TestCase("va\\ lue")]
    [TestCase("va~\\ lue")]
    [TestCase("va~\" lue")]
    [TestCase("va\\~ lue")]
    [TestCase("va~~\\ lue")]
    [TestCase("va\\~~ lue")]
    [TestCase("va\\\\ lue")]
    [TestCase("va\r\n\t lue")]
    [TestCase("va\r\\\n\t lue")]

    public void EscapeTest(string rawValue)
    {
      Console.WriteLine($"RawValue:'{rawValue}'");

      var c = TypedCommand.Create("test", null,
        CommandLine.KeyAndValueOption
      );

      var policyElement = new TextElement(null!, "elem_id", null, null, null, true, 100, false, false);

      var commandLine = c.Name + policyElement.ValueToCommandLine(rawValue);
      Console.WriteLine($"Cmd:'{commandLine}'");

      var args = CommandLineExtensions.CommandLineToArgs(commandLine);
      Console.WriteLine($"{args.Length} args: {string.Join(',', args.Select(a => $"'{a}'"))}");

      string? value = null;
      c.SetHandler((List<(string, List<string>)> keyValues) =>
      {
        var elemValues = keyValues.Find(e => e.Item1 == policyElement.Id);
        value = policyElement.ValueFromCommandLine(elemValues.Item2) as string;
      });

      c.Invoke(args);

      Console.WriteLine($"Value:'{value}'");
      value.Should().Be(rawValue);
    }
  }

  public class CommandLineTestCases : IEnumerable
  {
    public IEnumerator GetEnumerator()
    {
      var index = 1;
      yield return new TestCaseData("-h").SetName($"{index++:00} Help");
      yield return new TestCaseData("unknownCommand").SetName($"{index++:00} UnknownCommand");
      yield return new TestCaseData("-h enable").SetName($"{index++:00} Help enable");
      yield return new TestCaseData("-h search").SetName($"{index++:00} Help search");
      yield return new TestCaseData("interactive show inetres.MediaSettings User").SetName($"{index++:00} show");
      yield return new TestCaseData("enable inetres.MediaSettings User -k k1 -v v1 -k k2 -v v2 -v v3").SetName($"{index++:00} enable");
      yield return new TestCaseData("enable inetres.MediaSettings User -k k1 -v v1 -k k2 -v v2 v3 -v v4 -k k3 -v v5").SetName($"{index++:00} enable");
    }
  }
}
