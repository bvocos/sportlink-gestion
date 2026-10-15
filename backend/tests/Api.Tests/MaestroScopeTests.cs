using Api.Features.Maestros;
using Api.Shared.Database;

namespace Api.Tests;

public sealed class MaestroScopeTests
{
    [Fact]
    public void UpdatingProductDeposits_PreservesAssignmentsAndPrimaryDepositFromOtherBranches()
    {
        var visibleOld = Guid.NewGuid();
        var visibleNew = Guid.NewGuid();
        var hiddenPrimary = Guid.NewGuid();
        var product = new TipoCesped
        {
            Id = Guid.NewGuid(),
            DepositoId = hiddenPrimary,
            Depositos =
            [
                new TipoCespedDeposito { DepositoId = hiddenPrimary },
                new TipoCespedDeposito { DepositoId = visibleOld }
            ]
        };

        MaestroEndpoints.ApplyVisibleDepositAssignments(product, [visibleNew], [visibleOld, visibleNew]);

        Assert.Equal(hiddenPrimary, product.DepositoId);
        Assert.Contains(product.Depositos, x => x.DepositoId == hiddenPrimary);
        Assert.DoesNotContain(product.Depositos, x => x.DepositoId == visibleOld);
        Assert.Contains(product.Depositos, x => x.DepositoId == visibleNew);
    }

    [Fact]
    public void UpdatingProductDeposits_ReplacesVisiblePrimaryDeposit()
    {
        var visibleOld = Guid.NewGuid();
        var visibleNew = Guid.NewGuid();
        var product = new TipoCesped { Id = Guid.NewGuid(), DepositoId = visibleOld,
            Depositos = [new TipoCespedDeposito { DepositoId = visibleOld }] };

        MaestroEndpoints.ApplyVisibleDepositAssignments(product, [visibleNew], [visibleOld, visibleNew]);

        Assert.Equal(visibleNew, product.DepositoId);
    }
}
