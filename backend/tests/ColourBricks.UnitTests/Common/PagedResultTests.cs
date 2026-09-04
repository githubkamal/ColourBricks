using ColourBricks.Application.Common.Pagination;
using FluentAssertions;

namespace ColourBricks.UnitTests.Common;

public class PagedResultTests
{
    [Theory]
    [InlineData(1234, 50, 25)] // plan.md §7 worked example
    [InlineData(0, 50, 0)]
    [InlineData(50, 50, 1)]
    [InlineData(100, 50, 2)]
    [InlineData(101, 50, 3)]
    [InlineData(1, 50, 1)]
    public void PagedEnvelope_ReturnsCorrectTotalPages(int totalCount, int pageSize, int expectedPages)
    {
        PagedResult<string> result = PagedResult<string>.Create([], page: 1, pageSize: pageSize, totalCount: totalCount);

        result.TotalPages.Should().Be(expectedPages);
        result.TotalCount.Should().Be(totalCount);
        result.PageSize.Should().Be(pageSize);
        result.Page.Should().Be(1);
    }

    [Fact]
    public void Create_RejectsNonPositivePageAndPageSize()
    {
        FluentActions.Invoking(() => PagedResult<string>.Create([], page: 0, pageSize: 50, totalCount: 0))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => PagedResult<string>.Create([], page: 1, pageSize: 0, totalCount: 0))
            .Should().Throw<ArgumentOutOfRangeException>();
    }
}
