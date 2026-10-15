using Api.Features.Cuotas;
using Api.Shared.Database;

namespace Api.Tests;

public sealed class ReceiptNumberTests
{
    [Fact]
    public void ReceiptNumbers_AreIndependentByBranch()
    {
        var branchA = Guid.NewGuid();
        var branchB = Guid.NewGuid();
        var movements = new List<MovimientoCaja>();

        AddPayment(movements, branchA);
        AddPayment(movements, branchB);
        AddPayment(movements, branchA);

        Assert.Equal([1, 2], movements.Where(x => x.SucursalId == branchA).Select(x => x.NumeroRecibo));
        Assert.Equal([1], movements.Where(x => x.SucursalId == branchB).Select(x => x.NumeroRecibo));
    }

    [Fact]
    public void DeletingAnEarlierSale_DoesNotRenumberLaterReceipts()
    {
        var branch = Guid.NewGuid();
        var firstSale = Guid.NewGuid();
        var laterSale = Guid.NewGuid();
        var movements = new List<MovimientoCaja>();

        AddPayment(movements, branch, firstSale);
        var laterReceipt = AddPayment(movements, branch, laterSale);
        movements.RemoveAll(x => x.VentaId == firstSale);

        Assert.Equal(2, laterReceipt.NumeroRecibo);
        Assert.Equal(2, Assert.Single(movements).NumeroRecibo);
    }

    private static MovimientoCaja AddPayment(List<MovimientoCaja> movements, Guid branch, Guid? sale = null)
    {
        var maximum = movements.Where(x => x.SucursalId == branch).Max(x => (int?)x.NumeroRecibo);
        var movement = new MovimientoCaja
        {
            SucursalId = branch,
            VentaId = sale,
            CuotaId = Guid.NewGuid(),
            Tipo = TipoMovimiento.Ingreso,
            NumeroRecibo = CuotaEndpoints.NextReceiptNumber(maximum)
        };
        movements.Add(movement);
        return movement;
    }
}
