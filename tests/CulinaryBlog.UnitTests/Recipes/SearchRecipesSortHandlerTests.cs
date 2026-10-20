using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Recipes.Queries.SearchRecipes;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Domain.Interfaces;
using Xunit;

namespace CulinaryBlog.UnitTests.Recipes;

/// <summary>
/// FR-SRCH-003 — handler phải chuyển sort xuống repository (sắp xếp trong SQL, trước LIMIT) chứ
/// không tự sắp xếp lại danh sách đã bị cắt. Repository giả ghi lại tham số nhận được.
/// </summary>
public class SearchRecipesSortHandlerTests
{
    private sealed class CapturingRepository : IRecipeRepository
    {
        public RecipeSearchSort? Sort { get; private set; }
        public RecipeSearchFilters? Filters { get; private set; }
        public bool Called { get; private set; }

        public Task<IReadOnlyList<Recipe>> SearchRecipesAsync(
            string keyword, CancellationToken ct = default,
            RecipeSearchFilters? filters = null, RecipeSearchSort? sort = null)
        {
            Called = true;
            Filters = filters;
            Sort = sort;
            return Task.FromResult<IReadOnlyList<Recipe>>([]);
        }

        // Các thành viên khác không dùng trong test này.
        public Task<Recipe?> GetByIdForPublishingAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Recipe?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Recipe?> GetBySlugWithDetailsAsync(string slug, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> SlugExistsAsync(string slug, Guid? excludeRecipeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public void SetOriginalRowVersion(Recipe recipe, byte[] expectedRowVersion) => throw new NotSupportedException();
        public Task<Recipe?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Recipe entity, CancellationToken ct = default) => throw new NotSupportedException();
        public void Update(Recipe entity) => throw new NotSupportedException();
        public void Remove(Recipe entity) => throw new NotSupportedException();
        public IQueryable<Recipe> Query() => throw new NotSupportedException();
    }

    [Fact]
    public async Task Handle_ShouldPassParsedSortAndFiltersToRepository()
    {
        var repository = new CapturingRepository();
        var handler = new SearchRecipesQueryHandler(repository);

        await handler.Handle(
            new SearchRecipesQuery("pho", MaxCookTime: 30, MinServings: 2, Sort: "-createdAt"),
            CancellationToken.None);

        Assert.Equal(new RecipeSearchSort(RecipeSearchSortField.CreatedAt, true), repository.Sort);
        Assert.Equal(30, repository.Filters?.MaxCookTime);
        Assert.Equal(2, repository.Filters?.MinServings);
    }

    [Fact]
    public async Task Handle_ShouldUseRelevanceOrder_WhenSortIsMissing()
    {
        var repository = new CapturingRepository();
        var handler = new SearchRecipesQueryHandler(repository);

        await handler.Handle(new SearchRecipesQuery("pho"), CancellationToken.None);

        Assert.True(repository.Called);
        Assert.Null(repository.Sort);
    }

    [Fact]
    public async Task Handle_ShouldRejectInvalidSort_BeforeQueryingDatabase()
    {
        var repository = new CapturingRepository();
        var handler = new SearchRecipesQueryHandler(repository);

        await Assert.ThrowsAsync<InvalidSortException>(
            () => handler.Handle(new SearchRecipesQuery("pho", Sort: "authorId"), CancellationToken.None));
        Assert.False(repository.Called);
    }
}
