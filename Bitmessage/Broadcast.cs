﻿using System;
using System.Diagnostics;
using System.Security.Cryptography;
using Org.BouncyCastle.Asn1.X9;
using bitmessage.Crypto;
using bitmessage.network;
using System.Linq;

namespace bitmessage
{
	public class Broadcast
	{
		public Broadcast(){}
		public Broadcast(Inv inv)
		{
			int pos = 12;
			Version = inv.Payload.ReadVarInt(ref pos);
			AddressVersion = inv.Payload.ReadVarInt(ref pos);
			StreamNumber = inv.Payload.ReadVarInt(ref pos);
			BehaviorBitfield = inv.Payload.ReadUInt32(ref pos);
			PublicSigningKeyWitout4Prefics = inv.Payload.ReadBytes(ref pos, 64);
			PublicEncryptionKeyWitout4Prefics = inv.Payload.ReadBytes(ref pos, 64);
			AddressHash = inv.Payload.ReadBytes(ref pos, 20);
			EncodingType = (EncodingType) inv.Payload.ReadVarInt(ref pos);
			Msg = inv.Payload.ReadVarStr(ref pos);
			UInt64 signatureLength = inv.Payload.ReadVarInt(ref pos);
			Signature = inv.Payload.ReadBytes(ref pos, (int) signatureLength);

			MsgStatus = CheckSing();
		}
		public UInt64 Version { get; set; }
		public UInt64 AddressVersion { get; set; }
		public UInt64 StreamNumber { get; set; }
		public UInt32 BehaviorBitfield { get; set; }
		public byte[] PublicSigningKeyWitout4Prefics { get; set; }
		public byte[] PublicEncryptionKeyWitout4Prefics { get; set; }
		public byte[] AddressHash { get; set; }
		public EncodingType EncodingType { get; set; }
		public string Msg { get; set; }
		public byte[] Signature { get; set; }

		public MsgStatus MsgStatus { get; set; }

		private MsgStatus CheckSing()
		{
			#region Check address hash
			byte[] buff = new byte[PublicSigningKeyWitout4Prefics.Length + PublicEncryptionKeyWitout4Prefics.Length + 2];
			Buffer.BlockCopy(PublicSigningKeyWitout4Prefics, 0, buff, 1, PublicSigningKeyWitout4Prefics.Length);
			Buffer.BlockCopy(PublicEncryptionKeyWitout4Prefics, 0, buff, PublicSigningKeyWitout4Prefics.Length+2, PublicEncryptionKeyWitout4Prefics.Length);
			buff[0] = 4;
			buff[PublicSigningKeyWitout4Prefics.Length + 1] = 4;
			byte[] sha = new SHA512Managed().ComputeHash(buff);

			Debug.WriteLine(sha.ToHex());

			RIPEMD160  m = RIPEMD160.Create();
			byte[] ripemd160 = m.ComputeHash(sha);

			Debug.WriteLine(ripemd160.ToHex());

			if (!ripemd160.SequenceEqual(AddressHash))
				return MsgStatus.Invalid;

			#endregion Check address hash

			return MsgStatus.Valid;
		}
	}
}