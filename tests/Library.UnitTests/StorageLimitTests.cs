using Library.Domain.Constants;
using Library.Domain.Entities;
using Xunit;

namespace Library.UnitTests;

public class StorageLimitTests
{
    [Fact]
    public void User_Can_Add_File_When_Within_Limit()
    {
        var user = new ApplicationUser
        {
            StorageUsed = 100,
            StorageLimit = StorageConstants.UserStorageLimitBytes
        };
        Assert.True(user.HasStorageFor(50));
    }

    [Fact]
    public void User_Cannot_Exceed_500MB_Limit()
    {
        var user = new ApplicationUser
        {
            StorageLimit = StorageConstants.UserStorageLimitBytes
        };
        user.StorageUsed = StorageConstants.UserStorageLimitBytes;
        Assert.False(user.HasStorageFor(1));
    }

    [Fact]
    public void UserStorageLimit_Is_500_MB()
    {
        Assert.Equal(500L * 1024 * 1024, StorageConstants.UserStorageLimitBytes);
    }

    [Fact]
    public void MaxCategories_Is_20()
    {
        Assert.Equal(20, StorageConstants.MaxCategoryCount);
    }
}
