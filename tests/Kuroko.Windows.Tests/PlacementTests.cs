using Kuroko.App.Overlay;
using Kuroko.Platform.TextIntegration;
using Kuroko.Platform.Windowing;

namespace Kuroko.Windows.Tests;

public class PlacementTests
{
    private static readonly MonitorArea Screen = new(new PxRect(0, 0, 1920, 1040), 1.0);

    [Fact]
    public void A_pill_is_centered_over_the_card_box()
    {
        Assert.Equal(860, CardSpots.CenterIn(720, 480, 200, Screen, 8));
    }

    [Fact]
    public void A_pill_wider_than_the_card_is_centered_too()
    {
        Assert.Equal(660, CardSpots.CenterIn(720, 480, 600, Screen, 8));
    }

    [Theory]
    [InlineData(0, 8)]       // would start left of the screen
    [InlineData(1700, 1312)] // would end right of the screen: 1920 - 600 - 8
    public void A_centered_pill_stays_between_the_margins(int boxX, int x)
    {
        Assert.Equal(x, CardSpots.CenterIn(boxX, 480, 600, Screen, 8));
    }

    [Fact]
    public void A_pill_at_a_card_spot_on_the_left_is_centered_in_the_card_not_at_its_edge()
    {
        var anchor = new Anchor(new ScreenPoint(0, 0), 0, Screen, CardSpot.Left, 8, AlignWidthDip: 480);

        var (x, _) = anchor.PlaceWindow(new System.Windows.Size(200, 40));

        Assert.Equal(8 + 140, x);
    }

    [Fact]
    public void A_pill_following_the_mouse_is_centered_in_the_card_box_kept_on_screen()
    {
        // A 480 wide card at the mouse near the right edge is pushed to 1920 - 480 - 8; the pill sits in its middle.
        var anchor = new Anchor(new ScreenPoint(1900, 300), 22, Screen, MarginDip: 8, AlignWidthDip: 480);

        var (x, y) = anchor.PlaceWindow(new System.Windows.Size(200, 40));

        Assert.Equal((1432 + 140, 322), (x, y));
    }

    [Fact]
    public void Without_a_card_width_the_pill_starts_at_the_anchor()
    {
        var anchor = new Anchor(new ScreenPoint(100, 300), 22, Screen, MarginDip: 8);

        Assert.Equal((100, 322), anchor.PlaceWindow(new System.Windows.Size(200, 40)));
    }

    [Fact]
    public void A_card_wider_than_the_screen_is_cut_to_the_work_area_before_centering()
    {
        var anchor = new Anchor(new ScreenPoint(0, 0), 0, Screen, CardSpot.Center, 8, AlignWidthDip: 5000);

        var (x, _) = anchor.PlaceWindow(new System.Windows.Size(200, 40));

        Assert.Equal((1920 - 200) / 2, x);
    }
}
