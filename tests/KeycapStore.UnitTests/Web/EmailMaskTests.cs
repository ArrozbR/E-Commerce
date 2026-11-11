using KeycapStore.Application.Notifications;

namespace KeycapStore.UnitTests.Web;

public class EmailMaskTests
{
    [Theory]
    [InlineData("pedro3@test.com", "p****3@test.com")]
    [InlineData("ana.lima@gmail.com", "a****a@gmail.com")]
    [InlineData("jo@example.com", "j****@example.com")]
    [InlineData("x@example.com", "x****@example.com")]
    [InlineData("sem-arroba", "****")]
    public void Mask_KeepsTheFirstAndLastLetterAndTheDomain(string email, string expected)
    {
        Assert.Equal(expected, EmailMask.Mask(email));
    }
}
