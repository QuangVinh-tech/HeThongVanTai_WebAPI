using HeThongVanTai.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

public class ShippingRulesTests
{
    private readonly ShippingOptions _o = new();

    [Fact]
    public void VolumetricWeight_30x20x10_Is_1_2kg()
        => Assert.Equal(1.2m, ShippingRules.VolumetricWeight(30, 20, 10));

    [Fact]
    public void ChargeWeight_Uses_Larger_Of_Real_And_Volumetric()
    {
        Assert.Equal(12m, ShippingRules.ChargeWeight(5, 50, 40, 30));   // quy đổi 12 > thực 5
        Assert.Equal(20m, ShippingRules.ChargeWeight(20, 50, 40, 30));  // thực 20 > quy đổi 12
    }

    [Fact]
    public void Fee_Standard_100km_2kg()
        => Assert.Equal(27000m, ShippingRules.CalcFee(_o, "Standard", 100, 2, 0, false, false, false));

    [Fact]
    public void Fee_Express_100km_2kg()
        => Assert.Equal(35000m, ShippingRules.CalcFee(_o, "Express", 100, 2, 0, false, false, false));

    [Fact]
    public void Fee_Never_Below_Min()
        => Assert.Equal(20000m, ShippingRules.CalcFee(_o, "Standard", 10, 0.5m, 0, false, false, false));

    [Fact]
    public void Fee_Adds_Insurance_Over_1M()
        => Assert.Equal(37000m, ShippingRules.CalcFee(_o, "Standard", 100, 2, 2_000_000, false, false, false));

    [Theory]
    [InlineData("Pending", "PickedUp", true)]
    [InlineData("Pending", "Delivered", false)]
    [InlineData("Delivered", "Pending", false)]
    [InlineData("InTransit", "Returned", true)]
    public void CanMove_Follows_Flow(string from, string to, bool expected)
        => Assert.Equal(expected, ShippingRules.CanMove(from, to));
}