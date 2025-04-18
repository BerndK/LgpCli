using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Infrastructure;
using LgpCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace LgpCoreTests
{
  public class ServiceProviderTests
  {
#if !RELEASE
    [Test]
    public void DebugDependenciesTest()
    {
      var serviceCollection = new ServiceCollection();

      RegisterServices(serviceCollection);

      var serviceProvider = serviceCollection.BuildServiceProvider();

      var class1 = serviceProvider.GetRequiredService<IClass1>();
      var class2 = serviceProvider.GetRequiredService<IClass2>();
      var class3 = serviceProvider.GetRequiredService<IClass3>();
      var class4 = serviceProvider.GetRequiredKeyedService<IClass4>("Key1");
      var class5 = serviceProvider.GetRequiredService<IClass5>();
      var class6 = serviceProvider.GetRequiredService<IClass6>();
      var class7 = serviceProvider.GetRequiredService<IClass7>();
      var class8 = serviceProvider.GetRequiredService<IClass8>();
      var class9 = serviceProvider.GetRequiredService<IClass9>();

      var scope = serviceProvider.CreateScope();
      var class3Scoped = scope.ServiceProvider.GetRequiredService<IClass3>();

      Console.WriteLine(serviceProvider.DebugDependencies());

      Console.WriteLine("\r\nSCOPED\r\n");
      Console.WriteLine(scope.ServiceProvider.DebugDependencies());
      scope.Dispose();
    }

    private static void RegisterServices(ServiceCollection serviceCollection)
    {
      serviceCollection.AddSingleton<IClass1, Class1>();
      serviceCollection.AddTransient<IClass2, Class2>();
      serviceCollection.AddScoped<IClass3, Class3>();
      serviceCollection.AddKeyedSingleton<IClass4, Class4>("Key1");
      serviceCollection.AddSingleton<IClass5>(new Class5());
      serviceCollection.AddSingleton<IClass6>(provider => new Class6());
      serviceCollection.AddSingleton<IClass7, Class7>();
      serviceCollection.AddSingleton<IClass8, Class8>();
      serviceCollection.AddSingleton<ISubClass1, SubClass1>();
      serviceCollection.AddSingleton<SubClass2>();
      serviceCollection.AddSingleton<IClass9, Class9_1>();
      serviceCollection.AddSingleton<IClass9, Class9_2>();

    }
#endif
  }
  public class Class1 : IClass1 { }
  public class Class2 : IClass2 { }
  public class Class3 : IClass3 { }
  public class Class4 : IClass4 { }
  public class Class5 : IClass5 { }
  public class Class6 : IClass6 { }

  public class Class7 : IClass7
  {
    public Class7(ISubClass1 subClass1)
    {
    }
  }
  public class Class8 : IClass8
  {
    public Class8(SubClass2 subClass2)
    {
    }
  }
  public class Class9_1 : IClass9 { }
  public class Class9_2 : IClass9 { }
  public class SubClass1 : ISubClass1 { }
  public class SubClass2 : ISubClass2 { }

  public interface IClass1 { }
  public interface IClass2 { }
  public interface IClass3 { }
  public interface IClass4 { }
  public interface IClass5 { }
  public interface IClass6 { }
  public interface IClass7 { }
  public interface IClass8 { }
  public interface IClass9 { }
  public interface ISubClass1 { }
  public interface ISubClass2 { }
}
