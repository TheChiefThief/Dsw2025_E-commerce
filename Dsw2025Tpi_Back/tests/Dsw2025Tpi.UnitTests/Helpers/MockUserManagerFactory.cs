using Microsoft.AspNetCore.Identity;
using Moq;

namespace Dsw2025Tpi.UnitTests.Helpers;

public static class MockUserManagerFactory
{
    public static Mock<UserManager<IdentityUser>> Create()
    {
        var store = new Mock<IUserStore<IdentityUser>>();
        return new Mock<UserManager<IdentityUser>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }
}
