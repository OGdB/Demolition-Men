using Demolition.Online;
using NUnit.Framework;

namespace Demolition.Tests
{
    /// <summary>The invite link (docs/hit-plan.md §4.2): what a host shares, and what a browser
    /// build reads from the address it was opened with, which anyone can write.</summary>
    public class InviteLinkTests
    {
        private const string Play = "https://play.example.org";
        private const string Page = Play + "/1.0.0/";

        // ---------------- Room codes ----------------

        [Test]
        public void TheAlphabet_IsTheRelays_AndEveryCharacterOfItMakesACode()
        {
            // server/relay RelayServer.cs hands out 6 of these; the keypad is 8 x 4 of them.
            Assert.AreEqual("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", RoomCode.Alphabet);
            Assert.AreEqual(6, RoomCode.Length);
            foreach (char c in RoomCode.Alphabet)
                Assert.AreEqual(new string(c, RoomCode.Length), RoomCode.Clean(new string(c, RoomCode.Length)));
        }

        [Test]
        public void SmallLetters_AreTheSameCode()
        {
            Assert.AreEqual("HJK234", RoomCode.Clean("hjk234"));
            Assert.AreEqual("HJK234", InviteLink.Code(Page + "?r=hJk234"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("ABCDE")]
        [TestCase("ABCDEFG")]
        [TestCase("ABCDE0")] // the look-alikes the relay leaves out
        [TestCase("ABCDEO")]
        [TestCase("ABCDE1")]
        [TestCase("ABCDEI")]
        [TestCase("abcdei")]
        [TestCase("ABC EF")]
        [TestCase("ABCDE ")]
        [TestCase("ABCDE\n")]
        [TestCase("ABCDE\0")]
        [TestCase("ABCD%41")]
        [TestCase("ABCDE+")]
        [TestCase("ABCDE/")]
        [TestCase("<b>A")]
        [TestCase("АBCDEF")] // a Cyrillic A
        [TestCase("ABCDEſ")] // a long s, which capitalises to S
        [TestCase("ABCDEK")] // the Kelvin sign
        [TestCase("ＡＢＣＤＥＦ")] // full-width ABCDEF
        public void WhatIsNoRoomCode_IsNotCleaned_NotBuilt_AndNotRead(string code)
        {
            Assert.IsNull(RoomCode.Clean(code));
            Assert.IsNull(InviteLink.Build(Play, "1.0.0", code));
            Assert.IsNull(InviteLink.Code(Page + "?r=" + code));
        }

        // ---------------- Building ----------------

        [Test]
        public void ALink_IsThePlayUrl_TheVersionsFolder_AndTheRoom()
        {
            Assert.AreEqual("https://play.example.org/1.0.0/?r=ABCDEF", InviteLink.Build(Play, "1.0.0", "ABCDEF"));
            Assert.AreEqual("https://play.example.org/1.0.0/", InviteLink.Page(Play, "1.0.0"));
            Assert.AreEqual("http://localhost:8000/web/0.9.3-beta_2/?r=HJK234", InviteLink.Build("http://localhost:8000/web", "0.9.3-beta_2", "hjk234"));
        }

        [Test]
        public void WhatIsBuilt_IsWhatIsRead()
        {
            foreach (string code in new[] { "ABCDEF", "Z98765", "HJKLMN", "222222" })
                Assert.AreEqual(code, InviteLink.Code(InviteLink.Build(Play, "1.0.0", code)));
        }

        [TestCase("https://play.example.org/")]
        [TestCase("https://play.example.org//")]
        [TestCase("  https://play.example.org/ ")]
        public void ThePlayUrlsLastSlash_IsNotDoubled(string playUrl)
        {
            Assert.AreEqual(Page + "?r=ABCDEF", InviteLink.Build(playUrl, "1.0.0", "ABCDEF"));
        }

        [TestCase(null)] // GameConfig.playUrl is empty until the owner sets it: no SHARE button
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("play.example.org")]
        [TestCase("ftp://play.example.org")]
        [TestCase("javascript:alert(1)")]
        [TestCase("https://")]
        [TestCase("https://play.example.org/?a=1")]
        [TestCase("https://play.example.org/#top")]
        [TestCase("https://play example.org")]
        public void WithoutAPlayUrlALinkCanStandOn_NoLink(string playUrl)
        {
            Assert.IsNull(InviteLink.Page(playUrl, "1.0.0"));
            Assert.IsNull(InviteLink.Build(playUrl, "1.0.0", "ABCDEF"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(".")]
        [TestCase("..")]
        [TestCase("1.0/../..")]
        [TestCase("1.0 beta")]
        [TestCase("1.0?x=1")]
        [TestCase("1.0#x")]
        [TestCase("1%2e0")]
        [TestCase("1.0\n")]
        [TestCase("123456789012345678901234567890123")] // 33
        public void AVersionThatIsNoFoldersName_NoLink(string version)
        {
            Assert.IsNull(InviteLink.Page(Play, version));
            Assert.IsNull(InviteLink.Build(Play, version, "ABCDEF"));
        }

        // ---------------- Reading ----------------

        [TestCase("https://play.example.org/1.0.0/?r=ABCDEF")]
        [TestCase("https://play.example.org/?r=ABCDEF")] // whatever the folder: the room is all that is read
        [TestCase("https://play.example.org/1.0.0/index.html?r=ABCDEF")]
        [TestCase("https://play.example.org/1.0.0/?r=ABCDEF#top")]
        [TestCase("https://play.example.org/1.0.0/?utm_source=chat&r=ABCDEF&fbclid=IwAR0")] // what apps add to a link
        [TestCase("https://play.example.org/1.0.0/?r=ABCDEF&r=GHJKLM")] // the first decides
        [TestCase("?r=ABCDEF")]
        public void TheRoom_IsReadFromTheQuery(string url)
        {
            Assert.AreEqual("ABCDEF", InviteLink.Code(url));
        }

        [TestCase(null)] // not a browser build: Application.absoluteURL is empty
        [TestCase("")]
        [TestCase("https://play.example.org/1.0.0/")]
        [TestCase("https://play.example.org/1.0.0/?")]
        [TestCase("https://play.example.org/1.0.0/?r")]
        [TestCase("https://play.example.org/1.0.0/?r=")]
        [TestCase("https://play.example.org/1.0.0/?R=ABCDEF")]
        [TestCase("https://play.example.org/1.0.0/?xr=ABCDEF")]
        [TestCase("https://play.example.org/1.0.0/?r2=ABCDEF")]
        [TestCase("https://play.example.org/1.0.0/?code=ABCDEF")]
        [TestCase("https://play.example.org/1.0.0/?%72=ABCDEF")]
        [TestCase("https://play.example.org/1.0.0/?r=%41BCDEF")]
        [TestCase("https://play.example.org/1.0.0/?r=ABCDEF%20")]
        [TestCase("https://play.example.org/1.0.0/?r=ABCDEF;x=1")]
        [TestCase("https://play.example.org/1.0.0/?r=ABCDEF/../x")]
        [TestCase("https://play.example.org/1.0.0/?r=nope&r=ABCDEF")] // the first decides
        [TestCase("https://play.example.org/1.0.0/#r=ABCDEF")] // a fragment is not the query
        [TestCase("https://play.example.org/1.0.0/#?r=ABCDEF")]
        [TestCase("https://play.example.org/1.0.0/?a=1#&r=ABCDEF")]
        [TestCase("https://play.example.org/r=ABCDEF")] // nor is the path
        [TestCase("https://play.example.org/1.0.0/?next=https://elsewhere.example/?r=ABCDEF")]
        [TestCase("https://play.example.org/1.0.0/?r=<script>alert(1)</script>")]
        [TestCase("https://play.example.org/1.0.0/?r=ABCDEF\r\nX-Header: 1")]
        public void AnAddressWithoutARoomCodeInItsQuery_ReadsAsNoInvite(string url)
        {
            Assert.IsNull(InviteLink.Code(url));
        }

        [Test]
        public void ALongAddressOfJunk_IsReadThrough()
        {
            Assert.IsNull(InviteLink.Code(Page + "?" + new string('&', 200000)));
            Assert.IsNull(InviteLink.Code(Page + "?r=" + new string('A', 1000000)));
            Assert.IsNull(InviteLink.Code(new string('?', 200000)));
            Assert.AreEqual("ABCDEF", InviteLink.Code(Page + "?" + new string('&', 200000) + "r=ABCDEF"));
        }

        // ---------------- The owner's own copy ----------------

        [TestCase("https://play.example.org/1.0.0/?r=ABCDEF", Play)]
        [TestCase("https://play.example.org", "https://play.example.org/")]
        [TestCase("https://play.example.org?r=ABCDEF", Play)]
        [TestCase("https://play.example.org/#top", Play)]
        [TestCase("HTTPS://PLAY.EXAMPLE.ORG/1.0.0/", Play)]
        [TestCase("https://example.org/demolition/1.0.0/", "https://example.org/demolition")]
        public void APageUnderThePlayUrl_IsTheOwnersOwnCopy(string page, string playUrl)
        {
            Assert.IsTrue(InviteLink.IsOwnCopy(page, playUrl));
        }

        [TestCase(null, Play)]
        [TestCase("", Play)]
        [TestCase("https://html-classic.itch.zone/html/123/index.html", Play)] // a portal's frame
        [TestCase("https://play.example.org.elsewhere.net/1.0.0/", Play)]
        [TestCase("https://play.example.org:8443/1.0.0/", Play)]
        [TestCase("https://play.example.orgy/", Play)]
        [TestCase("https://elsewhere.net/?from=https://play.example.org/", Play)]
        [TestCase("https://example.org/demolition-men/", "https://example.org/demolition")]
        [TestCase("https://play.example.org/1.0.0/", "")] // no play URL: no copy is the owner's
        [TestCase("https://play.example.org/1.0.0/", null)]
        [TestCase("play.example.org/1.0.0/", "play.example.org")]
        public void AnyOtherPage_IsNot(string page, string playUrl)
        {
            Assert.IsFalse(InviteLink.IsOwnCopy(page, playUrl));
        }
    }
}
