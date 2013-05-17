﻿using System;
using System.Diagnostics;
using System.Security.Cryptography;
using SQLite;

namespace bitmessage.network
{
	[Table("inventory")]
	public class Payload
	{
		private const int AverageProofOfWorkNonceTrialsPerByte = 320;
		private const int PayloadLengthExtraBytes = 14000;
		private const int MaximumAgeOfAnObjectThatIAmWillingToAccept = 216000;
		private const int LengthOfTimeToHoldOnToAllPubkeys = 2419200; //Equals 4 weeks. You could make this longer if you want but making it shorter would not be advisable because there is a very small possibility that it could keep you from obtaining a needed pubkey for a period of time.

		private byte[] _hash;
		private MsgType _msgType;
		private string _msgTypeString;
		private DateTime _receivedTime = DateTime.UtcNow;
		private int _streamNumber = 1;

		#region for DB

		[PrimaryKey]
		
		public byte[] Hash
		{
			get
			{
				if (_hash == null)
				{
					_hash = new byte[32];
					using (var sha512 = new SHA512Managed())
						Buffer.BlockCopy(sha512.ComputeHash( sha512.ComputeHash(Data) ), 0, _hash, 0, 32);
				}
				return _hash;
			}
			set { _hash = value; }
		}

		public byte[] Data { get; set; }

		public string MsgTypeString
		{
			get { return _msgTypeString; }
			set
			{
				_msgTypeString = value;
				if (!Enum.TryParse(_msgTypeString, true, out _msgType))
					_msgType = MsgType.NotKnown;
			}
		}
		
		public int StreamNumber
		{
			get { return _streamNumber; }
			set { _streamNumber = value; }
		}

		public DateTime ReceivedTime
		{
			get { return _receivedTime; }
			set { _receivedTime = value; }
		}

		public void SaveAsync(SQLiteAsyncConnection db) { db.InsertAsync(this); }

		public Payload() {}

		#endregion for DB

		[Ignore]
		public MsgType MsgType
		{
			get { return _msgType; }
			set { _msgType = value; throw new NotImplementedException(); } // need change _msgTypeString
		}
		
		public Payload(string msgType, byte[] data)
		{
			Data = data;
			MsgTypeString = msgType;
		}

		[Ignore]
		public bool IsProofOfWorkSufficient
		{
			get
			{
				int pos;
				byte[] resultHash;
				using (var sha512 = new SHA512Managed())
				{
					byte[] buff = new byte[8 + 512/8];
					Buffer.BlockCopy(Data, 0, buff, 0, 8);

					pos = 8;
					byte[] initialHash = sha512.ComputeHash(Data.ReadBytes(ref pos, Data.Length - pos));
					Buffer.BlockCopy(initialHash, 0, buff, 8, initialHash.Length);
					resultHash = sha512.ComputeHash(sha512.ComputeHash(buff));
				}

				pos = 0;
				UInt64 pow = resultHash.ReadUInt64(ref pos);

				UInt64 target =
					(UInt64)
					((decimal) Math.Pow(2, 64)/((Data.Length + PayloadLengthExtraBytes)*AverageProofOfWorkNonceTrialsPerByte));

				Debug.WriteLine("ProofOfWork=" + (pow < target) + " pow=" + pow + " target=" + target + " lendth=" + Data.Length);

				if (pow < target) return true;
				return false;
			}
		}

		[Ignore]
		public int FirstByteAfterTime
		{
			get
			{
				int pos = 8;
				UInt32 embeddedTime32 = Data.ReadUInt32(ref pos);
				if (embeddedTime32 == 0)
					return 16;
				return 12;
			}
		}

		[Ignore]
		public UInt64 EmbeddedTime
		{
			get
			{
				int pos = 8;
				UInt32 embeddedTime32 = Data.ReadUInt32(ref pos);

				if (embeddedTime32==0)
				{
					pos = 8;
					UInt64 embeddedTime64 = Data.ReadUInt64(ref pos);
					return embeddedTime64;
				}
				return embeddedTime32;
			}
		}

		public byte[] Sha512
		{
			get
			{
				using (var sha512 = new SHA512Managed())
					return sha512.ComputeHash(Data);
			}
		}

		[Ignore]
		public int Length { get { return Data.Length; } }

		[Ignore]
		public bool IsEmbeddedTimeValid
		{
			get
			{
				Debug.WriteLine("embeddedTime = " + EmbeddedTime.FromUnix());

				if (EmbeddedTime > DateTime.UtcNow.ToUnix() + 10800)
					return false;

				if (MsgType == MsgType.Broadcast)
				{
					if (EmbeddedTime < DateTime.UtcNow.ToUnix() - MaximumAgeOfAnObjectThatIAmWillingToAccept)
						return false;
					return true;
				}
				if (MsgType == MsgType.Pubkey)
				{
					if (EmbeddedTime < DateTime.UtcNow.ToUnix() - LengthOfTimeToHoldOnToAllPubkeys)
						return false;
					return true;
				}
				return false;
			}
		}

		[Ignore]
		public bool IsLengthValid
		{
			get
			{
				if (MsgType == MsgType.Broadcast)
				{
					if (Length < 180)
						return false;
					return true;
				}
				if (MsgType == MsgType.Pubkey)
				{
					if ((Length < 146) || (Length > 600))
						return false;
					return true;
				}
				return false;
			}
		}

		[Ignore]
		public bool IsValid
		{
			get
			{
				if ((MsgType == MsgType.Broadcast) || (MsgType == MsgType.Pubkey))
					return (IsProofOfWorkSufficient && IsLengthValid && IsEmbeddedTimeValid);
				return true;
			}
		}
	}
}