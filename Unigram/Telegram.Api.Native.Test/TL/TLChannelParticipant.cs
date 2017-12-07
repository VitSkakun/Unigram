// <auto-generated/>
using System;
using Telegram.Api.Native.TL;

namespace Telegram.Api.TL
{
	public partial class TLChannelParticipant : TLChannelParticipantBase 
	{
		public Int32 Date { get; set; }

		public TLChannelParticipant() { }
		public TLChannelParticipant(TLBinaryReader from)
		{
			Read(from);
		}

		public override TLType TypeId { get { return TLType.ChannelParticipant; } }

		public override void Read(TLBinaryReader from)
		{
			UserId = from.ReadInt32();
			Date = from.ReadInt32();
		}

		public override void Write(TLBinaryWriter to)
		{
			to.WriteInt32(UserId);
			to.WriteInt32(Date);
		}
	}
}