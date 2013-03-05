﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using NTFSLib.Objects.Enums;

namespace NTFSLib.Objects.Attributes
{
    public class AttributeList : Attribute
    {
        public AttributeListItem[] Items { get; set; }

        public override AttributeResidentAllow AllowedResidentStates
        {
            get
            {
                return AttributeResidentAllow.Resident | AttributeResidentAllow.NonResident;
            }
        }

        internal override void ParseAttributeResidentBody(byte[] data, int maxLength, int offset)
        {
            base.ParseAttributeResidentBody(data, maxLength, offset);

            Debug.Assert(maxLength >= ResidentHeader.ContentLength);

            List<AttributeListItem> results = new List<AttributeListItem>();

            int pointer = offset;
            while (pointer + 26 <= offset + maxLength)      // 26 is the smallest possible MFTAttributeListItem
            {
                AttributeListItem item = AttributeListItem.ParseListItem(data, Math.Min(data.Length - pointer, maxLength), pointer);

                if (item.Type == AttributeType.EndOfAttributes)
                    break;

                results.Add(item);

                pointer += item.Length;
            }

            Items = results.ToArray();
        }

        internal override void ParseAttributeNonResidentBody(NTFS ntfs)
        {
            base.ParseAttributeNonResidentBody(ntfs);

            // Get all chunks
            byte[] data = Utils.ReadFragments(ntfs, NonResidentHeader.Fragments);
            
            // Parse
            List<AttributeListItem> results = new List<AttributeListItem>();

            int pointer = 0;
            while (pointer + 26 <= data.Length)     // 26 is the smallest possible MFTAttributeListItem
            {
                AttributeListItem item = AttributeListItem.ParseListItem(data, data.Length - pointer, pointer);

                if (item.Type == AttributeType.EndOfAttributes)
                    break;

                if (item.Length == 0)
                    break;

                results.Add(item);

                pointer += item.Length;
            }

            Debug.Assert(pointer == (int) NonResidentHeader.ContentSize);

            Items = results.ToArray();
        }
    }
}