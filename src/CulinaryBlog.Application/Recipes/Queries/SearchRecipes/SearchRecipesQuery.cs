using CulinaryBlog.Application.Recipes.Dtos;
using MediatR;
using CulinaryBlog.Domain.Enums;

namespace CulinaryBlog.Application.Recipes.Queries.SearchRecipes;

/// <summary>
/// FR-SRCH-001 tìm toàn văn, FR-SRCH-002 bộ lọc (Ý), FR-SRCH-003 sắp xếp (Thịnh).
/// <c>Sort</c>: tên field, thêm tiền tố "-" để giảm dần; null/rỗng = theo độ liên quan (ts_rank).
/// </summary>
public record SearchRecipesQuery(
    string Keyword,
    Guid? CategoryId = null,
    RecipeDifficulty? Difficulty = null,
    int? MinPrepTime = null,
    int? MaxPrepTime = null,
    int? MinCookTime = null,
    int? MaxCookTime = null,
    int? MinServings = null,
    string? Sort = null) : IRequest<IEnumerable<RecipeDto>>;
