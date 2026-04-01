using Warehouse_Desktop;

namespace Warehouse_Desktop.Tests;

public class ReservationTests
{
    private WarehouseDeskApp _app;
    private DateTime _now;

    [SetUp]
    public void Setup()
    {
        _app = new WarehouseDeskApp();
        _app.SeedData();
        _now = new DateTime(2026, 4, 1, 13, 17, 0);
    }

    [Test]
    public void Reserve_ValidQuantity_Success()
    {
        // Arrange
        // Initial STAPLER stock is 4

        // Act
        _app.ProcessLine("RESERVE;alice;STAPLER;2;10", _now);

        // Assert
        Assert.That(_app.EventLog, Has.Some.Contains("reserved 2 of STAPLER for alice"));
        
        _app.ProcessLine("COUNT;STAPLER", _now);
        Assert.That(_app.EventLog, Has.Some.Contains("count STAPLER onHand=4 reserved=2 available=2"));
    }

    [Test]
    public void Reserve_InsufficientStock_Fails()
    {
        // Arrange
        // Initial STAPLER stock is 4

        // Act
        _app.ProcessLine("RESERVE;bob;STAPLER;5;10", _now);

        // Assert
        Assert.That(_app.EventLog, Has.Some.Contains("cannot reserve 5 of STAPLER for bob: insufficient stock"));
    }

    [Test]
    public void Confirm_ValidReservation_ShipsOrder()
    {
        // Arrange
        _app.ProcessLine("RESERVE;alice;STAPLER;2;10", _now);
        // Assuming reservation ID is R1001 (based on nextOrderNumber style)

        // Act
        _app.ProcessLine("CONFIRM;R1001", _now);

        // Assert
        Assert.That(_app.EventLog, Has.Some.Contains("reservation R1001 confirmed and shipped"));
        _app.ProcessLine("COUNT;STAPLER", _now);
        // After shipping, onHand should decrease, and reserved should decrease
        Assert.That(_app.EventLog, Has.Some.Contains("count STAPLER onHand=2 reserved=0 available=2"));
    }

    [Test]
    public void Release_ValidReservation_ReturnsStock()
    {
        // Arrange
        _app.ProcessLine("RESERVE;alice;STAPLER;2;10", _now);

        // Act
        _app.ProcessLine("RELEASE;R1001", _now);

        // Assert
        Assert.That(_app.EventLog, Has.Some.Contains("reservation R1001 released"));
        _app.ProcessLine("COUNT;STAPLER", _now);
        Assert.That(_app.EventLog, Has.Some.Contains("count STAPLER onHand=4 reserved=0 available=4"));
    }

    [Test]
    public void Reservation_ExpiresAutomatically()
    {
        // Arrange
        _app.ProcessLine("RESERVE;alice;STAPLER;2;10", _now);
        
        // Act
        // Move time forward by 11 minutes
        DateTime later = _now.AddMinutes(11);
        _app.ProcessLine("COUNT;STAPLER", later);

        // Assert
        Assert.That(_app.EventLog, Has.Some.Contains("count STAPLER onHand=4 reserved=0 available=4"));
        Assert.That(_app.EventLog, Has.Some.Contains("reservation R1001 expired"));
    }

    [Test]
    public void Confirm_ExpiredReservation_Fails()
    {
        // Arrange
        _app.ProcessLine("RESERVE;alice;STAPLER;2;10", _now);
        DateTime later = _now.AddMinutes(11);

        // Act
        _app.ProcessLine("CONFIRM;R1001", later);

        // Assert
        Assert.That(_app.EventLog, Has.Some.Contains("cannot confirm R1001: reservation expired or not found"));
    }
}
