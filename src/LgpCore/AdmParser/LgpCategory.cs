using System;

namespace LgpCore.AdmParser
{
  public class LgpCategory
  {
    public LgpCategory(PolicyNamespace policyNamespace, string name, string displayName, AdmContent? content) : this(
      new CategoryIdent(policyNamespace, name), displayName, content)
    {}
    
    public LgpCategory(CategoryIdent categoryIdent, string displayName, AdmContent? content)
    {
      CategoryIdent = categoryIdent;
      Content = content;
      DisplayName = displayName;
    }

    public CategoryIdent CategoryIdent { get; }
    public AdmContent? Content { get; }
    public string Name => CategoryIdent.Name;
    public string DisplayName { get; set; }

    public LgpCategory? Parent { get; set; }

    public List<Policy> Policies { get; } = new List<Policy>();

    public List<LgpCategory> Items = new List<LgpCategory>();
  }

  public static class LgpCategoryBuilder
  {
    public static LgpCategory BuildRootCategory(this AdmFolder admFolder, out List<LgpCategory> categories)
    {
      var rootNamespace = new PolicyNamespace("ROOT", "ROOT");
      var rootCategoryIdent = new CategoryIdent(rootNamespace, "ROOT");
      var rootCategory = new LgpCategory(rootCategoryIdent, "Root", null);

      var invalidNamespace = new PolicyNamespace("INVALID", "INVALID");
      var invalidCategoryIdent = new CategoryIdent(invalidNamespace, "INVALID");
      var invalidCategory = new LgpCategory(invalidCategoryIdent, "InvalidCategory", null);
      invalidCategory.Parent = rootCategory;

      var allCategories = new Dictionary<Category, LgpCategory>();

      //1. first loop, build all categories
      foreach (var content in admFolder.Contents.Values)
      {
        //Console.WriteLine($"{content.TargetNamespace.prefix} {content.TargetNamespace.@namespace} ({content.Categories.Count})");
        foreach (var category in content.Categories.Values)
        {
          var categoryIdent = new CategoryIdent(content.TargetNamespace, category.Name);//e.g. "	Microsoft.Policies.ActiveXInstallService" "AxInstSv"
          allCategories.Add(category, new LgpCategory(categoryIdent, category.DisplayName, content));
          //Console.WriteLine($"  {category.name} '{content.GetLocatedString(category.displayName, ci)}' Parent:{category.parentCategory?.@ref ?? "**ROOT**"}");
        }
      }

      LgpCategory GetParentLgpCategory(AdmContent admContent, string? parentCategoryRef)
      {
        LgpCategory lgpCategory = invalidCategory;
        var parentContent = admContent.GetNsRelatedContent(parentCategoryRef, out var name);
        if (parentContent != null)
        {
          if (parentContent.Categories.TryGetValue(name, out var parentCategory))
          {

            lgpCategory = allCategories[parentCategory];
          }
        }

        return lgpCategory;
      }

      //2. second loop, set parent / items (and build policy dependencies)
      foreach (var content in admFolder.Contents.Values)
      {
        foreach (var category in content.Categories.Values)
        {
          //get LgpCategory for Category object
          var current = allCategories[category];
          
          if (category.ParentCategoryRef != null) //windows:WindowsComponents
          {
            var parentLgpCategory = GetParentLgpCategory(content, category.ParentCategoryRef);

            parentLgpCategory.Items.Add(current);
            current.Parent = parentLgpCategory;
          }
          else
          {
            //add to root
            rootCategory.Items.Add(current);
            current.Parent = rootCategory;
          }
        }

        foreach (var policy in content.Policies)
        {
          var parentLgpCategory = GetParentLgpCategory(content, policy.ParentCategoryRef);
          parentLgpCategory.Policies.Add(policy);
          policy.Category = parentLgpCategory;
        }
      }

      rootCategory.Items.Add(invalidCategory);



      ////Test namespace vs prefix
      //// Result: there is no 1:n relation
      //// there is one prefix 'apparmor' that has two namespaces
      //var groups = allCategories
      //  .Select(c => (c.Value.Namespace, c.Value.Prefix))
      //  .Distinct()
      //  .GroupBy(e => e.Namespace, e => e.Prefix)
      //  .ToList();

      //var suspect = groups
      //  .Where(e => e.Count() != 1)
      //  .ToList();

      //var groups = allCategories
      //  .Select(c => (c.Value.Namespace, c.Value.Prefix))
      //  .Distinct()
      //  .GroupBy(e => e.Prefix, e => e.Namespace)
      //  .ToList();

      //var suspect = groups
      //  .Where(e => e.Count() != 1)
      //  .ToList();

      categories = allCategories.Values.ToList();
      return rootCategory;
    }
  }

  public class CategoryIdent : IEquatable<CategoryIdent>
  {
    public CategoryIdent(PolicyNamespace policyNamespace, string name)
    {
      PolicyNamespace = policyNamespace;
      Name = name;
    }

    public PolicyNamespace PolicyNamespace { get; }
    public string Name { get; }

    public bool Equals(CategoryIdent? other)
    {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return PolicyNamespace.Equals(other.PolicyNamespace) && Name == other.Name;
    }

    public override bool Equals(object? obj)
    {
      if (ReferenceEquals(null, obj)) return false;
      if (ReferenceEquals(this, obj)) return true;
      if (obj.GetType() != this.GetType()) return false;
      return Equals((CategoryIdent) obj);
    }

    public override int GetHashCode()
    {
      return HashCode.Combine(PolicyNamespace, Name);
    }
  }
}
