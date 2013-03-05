﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using NTFSLib.Objects.Enums;
using NTFSLib.Objects.Headers;

namespace NTFSLib.Objects.Attributes
{
    public abstract class Attribute
    {
        public AttributeType Type { get; set; }
        public ushort TotalLength { get; set; }
        public ResidentFlag NonResidentFlag { get; set; }
        public byte NameLength { get; set; }
        public ushort OffsetToName { get; set; }
        public AttributeFlags Flags { get; set; }
        public ushort Id { get; set; }

        public AttributeResidentHeader ResidentHeader { get; set; }
        public AttributeNonResidentHeader NonResidentHeader { get; set; }

        public abstract AttributeResidentAllow AllowedResidentStates { get; }

        public static AttributeType GetType(byte[] data, int offset)
        {
            return (AttributeType)BitConverter.ToUInt32(data, offset);
        }

        public static uint GetLength(byte[] data, int offset)
        {
            return BitConverter.ToUInt16(data, offset + 4);
        }

        private void ParseHeader(byte[] data, int offset)
        {
            Debug.Assert(data.Length - offset >= 16);
            Debug.Assert(0 <= offset && offset <= data.Length);

            Type = (AttributeType)BitConverter.ToUInt32(data, offset);

            if (Type == AttributeType.EndOfAttributes)
                return;

            TotalLength = BitConverter.ToUInt16(data, offset + 4);
            NonResidentFlag = (ResidentFlag)data[offset + 8];
            NameLength = data[offset + 9];
            OffsetToName = BitConverter.ToUInt16(data, offset + 10);
            Flags = (AttributeFlags)BitConverter.ToUInt16(data, offset + 12);
            Id = BitConverter.ToUInt16(data, offset + 14);
        }

        internal virtual void ParseAttributeResidentBody(byte[] data, int maxLength, int offset)
        {
            Debug.Assert(NonResidentFlag == ResidentFlag.Resident);
            Debug.Assert(AllowedResidentStates.HasFlag(AttributeResidentAllow.Resident));

            Debug.Assert(data.Length - offset >= maxLength);
            Debug.Assert(0 <= offset && offset <= data.Length);
        }

        internal virtual void ParseAttributeNonResidentBody(NTFS ntfs)
        {
            Debug.Assert(NonResidentFlag == ResidentFlag.NonResident);
            Debug.Assert(AllowedResidentStates.HasFlag(AttributeResidentAllow.NonResident));
            Debug.Assert(ntfs != null);
        }

        public static Attribute ParseSingleAttribute(byte[] data, int maxLength, int offset = 0)
        {
            Debug.Assert(data.Length - offset >= maxLength);
            Debug.Assert(0 <= offset && offset <= data.Length);

            AttributeType type = GetType(data, offset);

            if (type == AttributeType.EndOfAttributes)
            {
                Attribute tmpRes = new AttributeGeneric();
                tmpRes.ParseHeader(data, offset);

                return tmpRes;
            }

            Attribute res;

            switch (type)
            {
                case AttributeType.Unknown:
                    res = new AttributeGeneric();
                    break;
                case AttributeType.STANDARD_INFORMATION:
                    res = new AttributeStandardInformation();
                    break;
                case AttributeType.ATTRIBUTE_LIST:
                    res = new AttributeList();
                    break;
                case AttributeType.FILE_NAME:
                    res = new AttributeFileName();
                    break;
                case AttributeType.OBJECT_ID:
                    // Also OBJECT_ID
                    // TODO: Handle either case
                    res = new AttributeObjectId();
                    break;
                case AttributeType.SECURITY_DESCRIPTOR:
                    res = new AttributeSecurityDescriptor();
                    break;
                case AttributeType.VOLUME_NAME:
                    res = new AttributeVolumeName();
                    break;
                case AttributeType.VOLUME_INFORMATION:
                    res = new AttributeVolumeInformation();
                    break;
                case AttributeType.DATA:
                    res = new AttributeData();
                    break;
                case AttributeType.INDEX_ROOT:
                    res = new AttributeIndexRoot();
                    break;
                case AttributeType.INDEX_ALLOCATION:
                    res = new AttributeIndexAllocation();
                    break;
                case AttributeType.BITMAP:
                    res = new AttributeBitmap();
                    break;
                case AttributeType.REPARSE_POINT:
                    // TODO
                    res = new AttributeGeneric();
                    break;
                case AttributeType.EA_INFORMATION:
                    res = new AttributeExtendedAttributeInformation();
                    break;
                case AttributeType.EA:
                    res = new AttributeExtendedAttriubtes();
                    break;
                // Property set seems to be obsolete
                //case AttributeType.PROPERTY_SET:
                //    res = new MFTAttributeGeneric();
                //    break;
                case AttributeType.LOGGED_UTILITY_STREAM:
                    res = new AttributeLoggedUtilityStream();
                    break;
                default:
                    // TODO
                    res = new AttributeGeneric();
                    break;
            }

            res.ParseHeader(data, offset);
            if (res.NonResidentFlag == ResidentFlag.Resident)
            {
                Debug.Assert(res.AllowedResidentStates.HasFlag(AttributeResidentAllow.Resident));

                res.ResidentHeader = AttributeResidentHeader.ParseHeader(res, data, offset + 16);

                int bodyOffset = offset + res.ResidentHeader.ContentOffset;
                int length = offset + res.TotalLength - bodyOffset;

                Debug.Assert(length >= res.ResidentHeader.ContentLength);
                Debug.Assert(offset + maxLength >= bodyOffset + length);

                res.ParseAttributeResidentBody(data, length, bodyOffset);
            }
            else if (res.NonResidentFlag == ResidentFlag.NonResident)
            {
                Debug.Assert(res.AllowedResidentStates.HasFlag(AttributeResidentAllow.NonResident));

                res.NonResidentHeader = AttributeNonResidentHeader.ParseHeader(res, data, offset + 16);

                int bodyOffset = offset + res.NonResidentHeader.ListOffset;
                int length = res.TotalLength - res.NonResidentHeader.ListOffset;

                Debug.Assert(offset + maxLength >= bodyOffset + length);

                res.NonResidentHeader.Fragments = DataFragment.ParseFragments(data, length, bodyOffset, res.NonResidentHeader.StartingVCN, res.NonResidentHeader.EndingVCN);
            }
            else
            {
                throw new NotImplementedException("Couldn't process residentflag");
            }

            return res;
        }
    }
}