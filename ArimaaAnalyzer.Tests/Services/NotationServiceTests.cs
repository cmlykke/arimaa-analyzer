using System.Threading.Tasks;
using ArimaaAnalyzer.Maui.Services;
using System;
using System.IO;
using FluentAssertions;
using Xunit;
using YourApp.Models;

namespace ArimaaAnalyzer.Tests.Services;


public class NotationServiceTests
{
    
    private static string[] BaseBoard => new[]
    {
        "rrrrrrrr",
        "hdcemcdh",
        "........",
        "........",
        "........",
        "........",
        "HDCMECDH",
        "RRRRRRRR"
    };
    
    private static string[] EmptyBoard => new[]
    {
        "........",
        "........",
        "........",
        "........",
        "........",
        "........",
        "........",
        "........"
    };

    private static string[] MixedBoard => new[]
    {
        ".rrr.rrr",
        "h.cemcdh",
        "........",
        ".d......",
        "....E...",
        "........",
        "HDCM.CDH",
        "R.RRR.RR"
    };
    
    private static string Game_base = @"1w Ed2 Mb2 Ha2 Hg2 Dd1 De2 Cb1 Cf2 Ra1 Rc1 Re1 Rf1 Rg1 Rh1 Rc2 Rh2\n
1b ra7 hb7 cc7 hd7 ee7 cf7 mg7 dh7 ra8 rb8 rc8 rd8 de8 rf8 rg8 rh8\n2w Ed2n Ed3n Ed4n Mb2n\n
2b hb7s ee7s dh7s ee6w\n3w Ed5w Ec5w Eb5e hb6s\n3b ed6s ed5s mg7s ed4w\n4w hb5s Ec5w Ha2n Ha3n\n
4b mg6s hd7s de8s dh6w\n5w Mb3s hb4s hb3w Mb2n\n5b mg5s ec4w Mb3e eb4s\n6w Eb5s Cb1n Ra1n Rc1w\n
6b Ra2s ha3s ra7s mg4e\n7w Ha4n Ha5s ra6s Dd1n\n7b eb3w Mc3w rb8s rb7s\n8w Dd2n Dd3w De2n Re1n\n
8b mh4s dg6s dg5s dg4w\n9w Eb4e Mb3n Dc3w Rc2n\n9b Cb2e ha2e df4e dg4n\n10w Rc3e Db3e Mb4s Ec4w\n
10b ea3s Rb1e hb2s ea2e\n11w Eb4n Eb5s rb6s\n11b dg5s mh3w dg4w rb5e\n12w Ha4s ra5s Eb4e ra4e\n
12b hd6s de7s hd5e he5s\n13w Ec4e he4n Ed4e Dc3n\n13b Cc2n eb2e ec2e he5e\n14w df4s Ee4e Mb3s rb4s\n
14b Re2s ed2e De3n ee2n\n15w Cc3s rb3e rc3x Mb2n hb1n\n15b Rd3w ee3w rh8s df3w\n16w Cf2w Hg2w Dc4w Rc3n\n
16b mg3w Hf2e mf3s de3e\n17w Hg2n df3w Ef4s Db4n\n17b hf5s ed3n Rc4s ed4w\n18w Re1w Rf1w mf2s Ef3s\n
18b rh7s rg8s rg7s rg6s\n19w Ce2w Ef2w Ee2e de3s\n19b rg5w ec4e ed4s ed3e\n20w Db5n Db6n cc7e Db7e\n
20b hf4e rf5s rf4s hg4w\n21w Rc3n Rc4w Rb4n Rb5n\n21b ee3w de6w rc8w rf3w\n22w Rb6n Ha3n Ha4n Ha5e\n
22b rd8w ed3n ed4w ec4w\n23w Cc2n Rc1n hb2s Mb3s\n23b dd6s dd5s dd4s cf7w\n24w Cc3n Rh2n Rh3n Rh4n\n
24b rf8w re3e ce7e re8s\n25w Hg3e rf3e rg3s Hh3w\n25b dd3e Cd2n cd7s re7w\n26w Rd1n Cd3n De4n De5w\n
26b Re1w de2s Hb5w eb4n\n27w cd6w Dd5n Cc4s rc5s cc6x\n27b de3w dd3e Cd4s cf7e\n
28w Dd6e rd7s rd6w rc6x De6w\n28b eb5s rc4e eb4n eb5n\n29w rd4w Cd3n Cc3w rc4s rc3x\n
29b eb6s Rb7s eb5e Rb6s\n30w Rb5n Rb6n Rc2n Rc3n\n30b ec5w eb5n Rb7w eb6n\n31w Rc4n Hg3w de3w Hf3w\n
31b rh6w rg6s rg5s rg4s\n32w Dd6e De6n De7e Rh5n\n32b Dc7s eb7e ec7e ed7e\n33w Dc6n rc8e Dc7n Ra7e\n
33b ee7w Df7w ed7w De7w\n34w Rc5e Rd5n Dd7e De7e\n34b hf4w he4n he5n he6n\n35w Rd6e He3e Hf3w rg3w rf3x\n
35b he7n Df7w De7w he8s\n36w He3e dd3e Hf3n de3e df3x\n36b Re6e Rf6x he7s he6w cg7e\n
37w Hf4n Hf5e Hg5n Hg6n\n37b hd6e de1n he6n he7e\n38w Hg7n ch7w cg7s Hg8s\n38b hf7n hf8e hg8e hh8s\n
39w cg6w cf6x Hg7s Cd4n Cd5w\n39b Rh6s hh7s Rh5s hh6s\n40w Rd2n Rd3n Rd4n Rd5n\n40b hh5w hg5s hg4w Rh4w\n
41w Rd6e Re6n Re7n";

    private static string Game_goldsetup = @"1w Ed2 Mb2 Ha2 Hg2 Dd1 De2 Cb1 Cf2 Ra1 Rc1 Re1 Rf1 Rg1 Rh1 Rc2 Rh2\n";
    
    private static string Game_silversetup = @"1w Ed2 Mb2 Ha2 Hg2 Dd1 De2 Cb1 Cf2 Ra1 Rc1 Re1 Rf1 Rg1 Rh1 Rc2 Rh2\n
1b ra7 hb7 cc7 hd7 ee7 cf7 mg7 dh7 ra8 rb8 rc8 rd8 de8 rf8 rg8 rh8\n";
    
    private static string Game_firstMoveAfterSetup = @"1w Ed2 Mb2 Ha2 Hg2 Dd1 De2 Cb1 Cf2 Ra1 Rc1 Re1 Rf1 Rg1 Rh1 Rc2 Rh2\n
1b ra7 hb7 cc7 hd7 ee7 cf7 mg7 dh7 ra8 rb8 rc8 rd8 de8 rf8 rg8 rh8\n2w Ed2n Ed3n Ed4n Mb2n\n
2b hb7s ee7s dh7s ee6w\n";
    
    [Fact(DisplayName = "NotationService.BoardToAei can convert to a string, and AeiToBoard back again on empty board")]
    public void ConvertToStringAndBack_emptyboard()
    {
        // before: base position, gold to move
        var singleString = NotationService.BoardToAei(EmptyBoard, Sides.Gold);

        singleString.Should().Be("setposition g \"                                                                \"");

        var newShapeBoard = NotationService.AeiToBoard(singleString);
        
        newShapeBoard.Should().BeEquivalentTo(EmptyBoard);
        
    }
    
    [Fact(DisplayName = "NotationService.BoardToAei can convert to a string, and AeiToBoard back again ")]
    public void ConvertToStringAndBack_startingboard()
    {
        // before: base position, gold to move
        var singleString = NotationService.BoardToAei(BaseBoard, Sides.Gold);

        singleString.Should().Be("setposition g \"rrrrrrrrhdcemcdh                                HDCMECDHRRRRRRRR\"");

        var newShapeBoard = NotationService.AeiToBoard(singleString);
        
        newShapeBoard.Should().BeEquivalentTo(BaseBoard);
        
    }
    
    [Fact(DisplayName = "NotationService.BoardToAei can convert to a string, and AeiToBoard back again On mixed Board")]
    public void ConvertToStringAndBack_mixedBoard()
    {
        // before: base position, gold to move
        var singleString = NotationService.BoardToAei(MixedBoard, Sides.Gold);

        singleString.Should().Be("setposition g \" rrr rrrh cemcdh         d          E           HDCM CDHR RRR RR\"");

        var newShapeBoard = NotationService.AeiToBoard(singleString);
        
        newShapeBoard.Should().BeEquivalentTo(MixedBoard);
        
    }
    
    [Fact(DisplayName = "NotationService test that an arimaa game can be converted to a AEI string")]
    public void ConvertGameNotationToStringAndBack_mixedBoard()
    {
        // before: base position, gold to move
        var singleString = NotationService.GameToAei(Game_base);

        singleString.Should().Be("setposition s \"RMRE RRRH  CDHH  CRd H   e    H e   h   e    hH R ch   hrRrrd   \"");

        var test = "";
    }
    
    [Fact(DisplayName = "NotationService test that gold setup can be converted to a AEI string")]
    public void ConvertGameNotationToStringAndBack_goldSetup()
    {
        // before: base position, gold to move
        var singleString = NotationService.GameToAei(Game_goldsetup);

        singleString.Should().Be("setposition s \"RCRDRRRRHMREDCH                                                 \"");

        var test = "";
    }
    
    [Fact(DisplayName = "NotationService test that silver setup can be converted to a AEI string")]
    public void ConvertGameNotationToStringAndBack_silverSetup()
    {
        // before: base position, gold to move
        var singleString = NotationService.GameToAei(Game_silversetup);

        singleString.Should().Be("setposition g \"RCRDRRRRHMREDCH                                 rhchecmrrrrrdrr \"");

        var test = "";
    }
    
    [Fact(DisplayName = "NotationService test that first move after setup can be converted to a AEI string")]
    public void ConvertGameNotationToStringAndBack_firstMoveSetup()
    {
        // before: base position, gold to move
        var singleString = NotationService.GameToAei(Game_firstMoveAfterSetup);

        singleString.Should().Be("setposition g \"RMRERRRRH REDCH    E                            rhchecmdrrrrdrr \"");

        var test = "";
    }
    
    [Fact(DisplayName = "NotationService parses a game into a main-line turn tree")]
    public void ConvertGameNotationToListOfTupples_mixedBoard()
    {
        // before: base position, gold to move
        GameTurn? root = NotationService.ExtractTurnsWithMoves(Game_base);

        root.Should().NotBeNull();

        // Enumerate main line by following IsMainLine children
        var main = new List<GameTurn>();
        var node = root;
        while (node != null)
        {
            main.Add(node);
            node = node.Children.FirstOrDefault(c => c.IsMainLine);
        }

        main.Count.Should().Be(82);
        
        main[5].MoveNumber.ToString().Should().Be("3");
        main[5].Side.Should().Be(Sides.Gold);
        main[5].Moves[0].Should().Be("Ed5w");

        var test = "";
    }
    
    [Fact(DisplayName = "NotationService test that a AEI string can be generated")]
    public void ConvertGameArrayAndIndexToAEI_mixedBoard()
    {
        // before: base position, gold to move
        GameTurn? root = NotationService.ExtractTurnsWithMoves(Game_base);
        root.Should().NotBeNull();

        var aeiFromTurn_0 = NotationService.GameToAeiAtTurn(root!, 0);
        aeiFromTurn_0.Should().Be("setposition g \"                                                                \"");
        //aeiFromTurn_0.Should().Be("setposition s \"RCRDRRRRHMREDCHR                                                \"");
        
        var aeiFromTurn_1 = NotationService.GameToAeiAtTurn(root!, 1);
        aeiFromTurn_1.Should().Be("setposition s \"RCRDRRRRHMREDCHR                                                \"");
        
        var aeiFromTurn_2 = NotationService.GameToAeiAtTurn(root!, 2);
        aeiFromTurn_2.Should().Be("setposition g \"RCRDRRRRHMREDCHR                                rhchecmdrrrrdrrr\"");
        //aeiFromTurn_2.Should().Be("setposition s \"RMRERRRRH REDCHR   E                            rhchecmdrrrrdrrr\"");

        var aeiFromTurn_80 = NotationService.GameToAeiAtTurn(root!, 80);
        aeiFromTurn_80.Should().Be("setposition g \"  RR  HRH RR H RHCRR H   e RhhR HeC h   r D   h  ReD   hrR rh Hh\"");

        var test = "";
    }
    
    [Fact(DisplayName = "NotationService test that a AEI string can be generated")]
    public void ConvertGameArrayAndIndexToAEI_mixedBoard_testInternalAEIstr()
    {
        // before: base position, gold to move
        GameTurn? root = NotationService.ExtractTurnsWithMoves(Game_base);
        root.Should().NotBeNull();

        var aeiFromTurn_0 = root.Children.First().AEIstring;
        aeiFromTurn_0.Should().Be("setposition s \"RCRDRRRRHMREDCHR                                                \"");
        
        var aeiFromTurn_1 = root.Children.First().Children.First().AEIstring;
        aeiFromTurn_1.Should().Be("setposition g \"RCRDRRRRHMREDCHR                                rhchecmdrrrrdrrr\"");
        
        var aeiFromTurn_2 = root.Children.First().Children.First().Children.First().AEIstring;
        aeiFromTurn_2.Should().Be("setposition s \"RCRDRRRRH R DCHR M                 E            rhchecmdrrrrdrrr\"");
        
        

        //var aeiFromTurn_80 = NotationService.GameToAeiAtTurn(root!, 80);
        //aeiFromTurn_80.Should().Be("setposition s \"  RR  HRH RR H RHCRR H   e Rhhr heC R   R D R h  Red   hrr Rh Hh\"");

        var test = "";
    }
}