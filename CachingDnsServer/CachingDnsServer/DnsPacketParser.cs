using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace DnsServerGUI
{
    public class DnsHeader
    {
        public ushort Id;
        public bool IsResponse;
        public ushort OpCode;
        public bool Authoritative;
        public bool Truncation;
        public bool RecursionDesired;
        public bool RecursionAvailable;
        public ushort QuestionCount;
        public ushort AnswerCount;
        public ushort AuthorityCount;
        public ushort AdditionalCount;
    }

    public class DnsQuestion
    {
        public string Name;
        public ushort Type;
        public ushort Class;
    }

    public class DnsResourceRecord
    {
        public string Name;
        public ushort Type;
        public ushort Class;
        public uint TTL;
        public byte[] RData;
    }

    public static class DnsPacketParser
    {
        public static Tuple<DnsHeader, DnsQuestion> ParseQuery(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                DnsHeader header = ReadHeader(reader);
                string qname = ReadName(reader);
                ushort qtype = ReadUInt16BigEndian(reader);
                ushort qclass = ReadUInt16BigEndian(reader);
                DnsQuestion question = new DnsQuestion { Name = qname, Type = qtype, Class = qclass };
                return Tuple.Create(header, question);
            }
        }

        public static Tuple<DnsHeader, List<DnsQuestion>, List<DnsResourceRecord>> ParseResponse(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                DnsHeader header = ReadHeader(reader);
                List<DnsQuestion> questions = new List<DnsQuestion>();
                for (int i = 0; i < header.QuestionCount; i++)
                {
                    string name = ReadName(reader);
                    ushort type = ReadUInt16BigEndian(reader);
                    ushort cls = ReadUInt16BigEndian(reader);
                    questions.Add(new DnsQuestion { Name = name, Type = type, Class = cls });
                }
                List<DnsResourceRecord> answers = new List<DnsResourceRecord>();
                for (int i = 0; i < header.AnswerCount; i++)
                {
                    string name = ReadName(reader);
                    ushort type = ReadUInt16BigEndian(reader);
                    ushort cls = ReadUInt16BigEndian(reader);
                    uint ttl = ReadUInt32BigEndian(reader);
                    ushort rdlength = ReadUInt16BigEndian(reader);
                    byte[] rdata = reader.ReadBytes(rdlength);
                    answers.Add(new DnsResourceRecord { Name = name, Type = type, Class = cls, TTL = ttl, RData = rdata });
                }
                return Tuple.Create(header, questions, answers);
            }
        }

        public static byte[] BuildQuery(string domain)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                ushort id = (ushort)new Random().Next(0, 65535);
                WriteUInt16BigEndian(writer, id);
                WriteUInt16BigEndian(writer, 0x0100);
                WriteUInt16BigEndian(writer, 1);
                WriteUInt16BigEndian(writer, 0);
                WriteUInt16BigEndian(writer, 0);
                WriteUInt16BigEndian(writer, 0);

                WriteName(writer, domain);
                WriteUInt16BigEndian(writer, 1);
                WriteUInt16BigEndian(writer, 1);

                return ms.ToArray();
            }
        }

        public static byte[] BuildResponse(DnsHeader requestHeader, DnsQuestion question, List<DnsResourceRecord> answers, bool authoritative, bool nxdomain)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                WriteUInt16BigEndian(writer, requestHeader.Id);
                ushort flags = 0x8000;
                if (authoritative) flags |= 0x0400;
                if (nxdomain) flags |= 0x0003;
                WriteUInt16BigEndian(writer, flags);
                WriteUInt16BigEndian(writer, 1);
                WriteUInt16BigEndian(writer, (ushort)(nxdomain ? 0 : answers.Count));
                WriteUInt16BigEndian(writer, 0);
                WriteUInt16BigEndian(writer, 0);

                WriteName(writer, question.Name);
                WriteUInt16BigEndian(writer, question.Type);
                WriteUInt16BigEndian(writer, question.Class);

                if (!nxdomain)
                {
                    foreach (var rr in answers)
                    {
                        WriteName(writer, rr.Name);
                        WriteUInt16BigEndian(writer, rr.Type);
                        WriteUInt16BigEndian(writer, rr.Class);
                        WriteUInt32BigEndian(writer, rr.TTL);
                        WriteUInt16BigEndian(writer, (ushort)rr.RData.Length);
                        writer.Write(rr.RData);
                    }
                }
                return ms.ToArray();
            }
        }

        private static DnsHeader ReadHeader(BinaryReader reader)
        {
            DnsHeader header = new DnsHeader();
            header.Id = ReadUInt16BigEndian(reader);
            ushort flags = ReadUInt16BigEndian(reader);
            header.IsResponse = (flags & 0x8000) != 0;
            header.Authoritative = (flags & 0x0400) != 0;
            header.Truncation = (flags & 0x0200) != 0;
            header.RecursionDesired = (flags & 0x0100) != 0;
            header.RecursionAvailable = (flags & 0x0080) != 0;
            header.QuestionCount = ReadUInt16BigEndian(reader);
            header.AnswerCount = ReadUInt16BigEndian(reader);
            header.AuthorityCount = ReadUInt16BigEndian(reader);
            header.AdditionalCount = ReadUInt16BigEndian(reader);
            return header;
        }

        private static string ReadName(BinaryReader reader)
        {
            MemoryStream ms = (MemoryStream)reader.BaseStream;
            byte[] buffer = ms.ToArray();
            int originalPosition = (int)ms.Position;
            int offset = originalPosition;
            List<string> labels = new List<string>();
            bool jumped = false;
            int jumpCount = 0;

            while (true)
            {
                if (jumpCount > buffer.Length) throw new FormatException();
                byte length = buffer[offset];
                if ((length & 0xC0) == 0xC0)
                {
                    int pointer = ((length & 0x3F) << 8) | buffer[offset + 1];
                    if (!jumped) { ms.Position = offset + 2; jumped = true; }
                    jumpCount++;
                    offset = pointer;
                    continue;
                }
                if (length == 0)
                {
                    if (!jumped) ms.Position = offset + 1;
                    break;
                }
                offset++;
                string label = Encoding.ASCII.GetString(buffer, offset, length);
                labels.Add(label);
                offset += length;
            }
            return string.Join(".", labels);
        }

        private static void WriteName(BinaryWriter writer, string name)
        {
            string[] parts = name.Split('.');
            foreach (var part in parts)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(part);
                writer.Write((byte)bytes.Length);
                writer.Write(bytes);
            }
            writer.Write((byte)0);
        }

        private static ushort ReadUInt16BigEndian(BinaryReader reader)
        {
            short val = IPAddress.NetworkToHostOrder(reader.ReadInt16());
            return (ushort)val;
        }

        private static void WriteUInt16BigEndian(BinaryWriter writer, ushort value)
        {
            short val = IPAddress.HostToNetworkOrder((short)value);
            writer.Write(val);
        }

        private static uint ReadUInt32BigEndian(BinaryReader reader)
        {
            int val = IPAddress.NetworkToHostOrder(reader.ReadInt32());
            return (uint)val;
        }

        private static void WriteUInt32BigEndian(BinaryWriter writer, uint value)
        {
            int val = IPAddress.HostToNetworkOrder((int)value);
            writer.Write(val);
        }
    }
}
