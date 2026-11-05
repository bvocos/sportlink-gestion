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

    [Fact]
    public void Usuario_de_buenos_aires_ve_el_deposito_de_su_sucursal_en_el_producto()
    {
        var buenosAires = Guid.NewGuid();
        var sanFrancisco = Guid.NewGuid();
        var product = new TipoCesped
        {
            DepositoId = buenosAires,
            Depositos =
            [
                new TipoCespedDeposito { DepositoId = sanFrancisco },
                new TipoCespedDeposito { DepositoId = buenosAires }
            ]
        };

        var visible = MaestroEndpoints.VisibleAssignedDepositIds(product, new HashSet<Guid> { buenosAires });

        Assert.Equal([buenosAires], visible);
    }
}
