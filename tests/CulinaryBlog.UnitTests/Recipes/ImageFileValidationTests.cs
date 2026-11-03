using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Recipes.Commands.UploadRecipeImage;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

public class ImageFileValidationTests
{
    [Theory]
    [InlineData("dish.png", "image/png", "89504E470D0A1A0A")]
    [InlineData("dish.jpg", "image/jpeg", "FFD8FFE00000")]
    [InlineData("dish.jpeg", "image/jpeg", "FFD8FF00")]
    [InlineData("dish.webp", "image/webp", "524946460000000057454250")]
    [InlineData("dish.avif", "image/avif", "00000018667479706176696600000000")]
    public void RecognizesSupportedHeaders(string name, string type, string hex)
    {
        Assert.StartsWith(".", RecipeImageFile.Validate(name, type, Convert.FromHexString(hex)));
    }

    [Theory]
    [InlineData("dish.png", "image/png", "3C7363726970743E")]
    [InlineData("dish.jpg", "image/jpeg", "89504E470D0A1A0A")]
    [InlineData("dish.gif", "image/gif", "474946383961")]
    [InlineData("dish.svg", "image/svg+xml", "3C7376673E")]
    [InlineData("dish.avif", "image/avif", "000000186674797069736F6D61766966")]
    public void RejectsMismatchesAndUnsupportedFormats(string name, string type, string hex)
        => Assert.Throws<InvalidFileException>(() => RecipeImageFile.Validate(name, type, Convert.FromHexString(hex)));

    [Fact]
    public void SizeBoundaryIsFiveMebibytes()
    {
        byte[] file = new byte[RecipeImageFile.MaxBytes];
        Convert.FromHexString("89504E470D0A1A0A").CopyTo(file,0);
        Assert.Equal(".png", RecipeImageFile.Validate("dish.png","image/png",file));
        Assert.Throws<InvalidFileException>(() => RecipeImageFile.Validate("dish.png","image/png",new byte[file.Length+1]));
        Assert.Throws<InvalidFileException>(() => RecipeImageFile.Validate("dish.png","image/png",[]));
    }
}
