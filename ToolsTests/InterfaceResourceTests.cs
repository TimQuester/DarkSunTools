using DSunTools.Common.UI;
using System;
using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace Dsun.Common.Tests
{
    public class InterfaceResourceTests
    {
        [Fact]
        public void ButnRoundTripsEditableFields()
        {
            var bytes = new byte[110];
            Encoding.ASCII.GetBytes("BUTN").CopyTo(bytes, 0);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), bytes.Length);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 2048);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), 127);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 12);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(100), 2048);

            ButnResource button = ButnResource.Parse(bytes);
            Assert.Equal(2048, button.Number);
            Assert.Equal(127, button.Width);
            Assert.Equal(2048u, button.IconId);

            button.Width = 160;
            button.Text = "START GAME";
            ButnResource roundTrip = ButnResource.Parse(button.Build());

            Assert.Equal(160, roundTrip.Width);
            Assert.Equal("START GAME", roundTrip.Text);
        }

        [Fact]
        public void WindRoundTripsItemPositions()
        {
            var bytes = new byte[261 + 30];
            Encoding.ASCII.GetBytes("WIND").CopyTo(bytes, 0);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), bytes.Length);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 3000);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(243), 1);

            int item = 261;
            Encoding.ASCII.GetBytes("BUTN").CopyTo(bytes, item + 4);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(item + 8), 2048);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(item + 12), 94);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(item + 14), 70);

            WindResource window = WindResource.Parse(bytes);
            Assert.Single(window.Items);
            Assert.Equal((short)94, window.Items[0].X);
            Assert.Equal(2048u, window.Items[0].Id);

            window.Items[0].X = 80;
            window.Items[0].Y = 60;
            WindResource roundTrip = WindResource.Parse(window.Build());

            Assert.Equal((short)80, roundTrip.Items[0].X);
            Assert.Equal((short)60, roundTrip.Items[0].Y);
        }
    }
}
