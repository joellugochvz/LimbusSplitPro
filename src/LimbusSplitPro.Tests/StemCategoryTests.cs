using Xunit;
using LimbusSplitPro.Core.Models;

namespace LimbusSplitPro.Tests;

public class StemCategoryTests
{
    [Fact]
    public void TestDefaultCategoriesContainsMandatoryStems()
    {
        var categories = StemCategory.CreateDefaultCategories();
        
        Assert.NotEmpty(categories);
        Assert.Contains(categories, c => c.Id == "vocals");
        Assert.Contains(categories, c => c.Id == "drums");
        Assert.Contains(categories, c => c.Id == "bass");
        Assert.Contains(categories, c => c.Id == "other");
    }

    [Fact]
    public void TestSubStemsHaveParents()
    {
        var categories = StemCategory.CreateDefaultCategories();
        var subStems = categories.Where(c => c.IsSubStem).ToList();

        Assert.NotEmpty(subStems);
        foreach (var sub in subStems)
        {
            Assert.False(string.IsNullOrEmpty(sub.ParentCategoryKey));
            Assert.Contains(categories, c => c.Id == sub.ParentCategoryKey);
        }
    }
}
