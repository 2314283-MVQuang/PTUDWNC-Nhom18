namespace CulinaryBlog.Domain.Common.Exceptions;

public sealed class RecipePublishIncompleteException(
    bool hasSteps,
    bool hasIngredients)
    : Exception(BuildMessage(hasSteps, hasIngredients))
{
    private static string BuildMessage(
        bool hasSteps,
        bool hasIngredients)
    {
        if (!hasSteps && !hasIngredients)
        {
            return "Công thức phải có ít nhất 1 bước thực hiện và 1 nguyên liệu để xuất bản.";
        }

        if (!hasSteps)
        {
            return "Công thức phải có ít nhất 1 bước thực hiện để xuất bản.";
        }

        return "Công thức phải có ít nhất 1 nguyên liệu để xuất bản.";
    }
}