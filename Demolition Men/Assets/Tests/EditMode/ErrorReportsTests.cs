using Demolition.Meta;
using NUnit.Framework;
using UnityEngine;

namespace Demolition.Tests
{
    /// <summary>The error reports: what of an exception is sent, how often, and without the device's folders.</summary>
    public class ErrorReportsTests
    {
        private const string Null = "NullReferenceException: Object reference not set to an instance of an object";
        private const string Stack = "Demolition.Run.RunHud.LateUpdate ()\n";

        private ErrorReports _reports;

        [SetUp]
        public void SetUp() => _reports = new ErrorReports();

        private ErrorReport Take(string msg, string stack, string[] paths = null) =>
            _reports.Take(msg, stack, "1.0.3", "android", "samsung SM-A515F", paths);

        [Test]
        public void AReport_IsTheseFieldsOnly_AsTheRelayTakesThem()
        {
            // server/relay's ErrorsTests posts this same text.
            Assert.AreEqual(@"{""v"":""1.0.3"",""p"":""android"",""device"":""samsung SM-A515F"",""msg"":""NullReferenceException: Object reference not set to an instance of an object"",""stack"":""Demolition.Run.RunHud.LateUpdate ()\n""}",
                JsonUtility.ToJson(Take(Null, Stack)));
        }

        [Test]
        public void OneKind_GoesOncePerSession_EvenWithAnotherMessage()
        {
            Assert.IsNotNull(Take(Null, Stack));
            Assert.IsNull(Take(Null, Stack));
            Assert.IsNull(Take("NullReferenceException: other words", Stack), "the same type and stack: the same bug");
            Assert.IsNotNull(Take(Null, "Somewhere.Else ()\n"));
            Assert.IsNotNull(Take("IndexOutOfRangeException: Index was outside the bounds of the array.", Stack));
        }

        [Test]
        public void ASession_SendsAtMostSoManyKinds()
        {
            for (int i = 0; i < ErrorReports.MaxPerSession; i++)
                Assert.IsNotNull(Take(Null, "Frame" + i + " ()\n"));
            Assert.IsNull(Take(Null, "OneMore ()\n"));
        }

        [Test]
        public void TheDevicesFolders_AreTakenOut_HoweverWritten()
        {
            string[] paths = { "C:/Users/Jan de Vries", "C:/Users/Jan de Vries/AppData/LocalLow/OG Studio/Demolition Men/", null, "", "/" };
            ErrorReport report = Take(
                @"IOException: Sharing violation on path C:\Users\Jan de Vries\AppData\LocalLow\OG Studio\Demolition Men\profile.json",
                "at c:/users/jan de vries/Desktop/x.cs\n", paths);
            Assert.AreEqual("IOException: Sharing violation on path <path>\\profile.json", report.msg, "the save folder as a whole, before the home folder");
            Assert.AreEqual("at <path>/Desktop/x.cs\n", report.stack);
            StringAssert.DoesNotContain("Jan", report.msg + report.stack);
        }

        [Test]
        public void LongText_IsCut_AndControlCharactersGo()
        {
            ErrorReport report = Take("Boom: " + new string('x', 900) + "\u0001", new string('y', 9000), null);
            Assert.AreEqual(ErrorReports.MaxMessage, report.msg.Length);
            Assert.AreEqual(ErrorReports.MaxStack, report.stack.Length);

            report = _reports.Take("Bad: a\u0000b\r\nc", "s ()\r\n", "1.0.3", "pc", "Box\nModel" + new string('z', 90), null);
            Assert.AreEqual("Bad: a b\nc", report.msg);
            Assert.AreEqual("s ()\n", report.stack);
            Assert.AreEqual(ErrorReports.MaxDevice, report.device.Length);
            StringAssert.DoesNotContain("\n", report.device);
        }

        [Test]
        public void NoMessage_IsNotSent()
        {
            Assert.IsNull(Take("", Stack));
            Assert.IsNull(Take(null, Stack));
            Assert.IsNull(Take("  ", Stack));
        }

        [Test]
        public void TheKind_IsTheTypeBeforeTheColon()
        {
            Assert.AreEqual("NullReferenceException", ErrorReports.Kind(Null));
            Assert.AreEqual("no colon here", ErrorReports.Kind("no colon here"));
        }
    }
}
