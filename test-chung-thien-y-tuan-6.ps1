# Dung lai ha tang test PostgreSQL/Redis TAM; khong truy cap database nhom hay LMS.
& (Join-Path $PSScriptRoot 'test-chung-thien-y-tuan-1-5.ps1') `
    -TestFilter 'FullyQualifiedName~CulinaryBlog.UnitTests.Recipes.RecipeApiTests.Search_'
exit $LASTEXITCODE
