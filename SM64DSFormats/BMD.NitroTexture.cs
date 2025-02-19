﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Drawing;
using QuickGraph;

namespace SM64DSe.SM64DSFormats
{
    using Pal3To4Edge = TaggedEdge<NitroTexture.Palette, int>;
    using Pal3To4Map = BidirectionalGraph<NitroTexture.Palette, 
        TaggedEdge<NitroTexture.Palette, int>>;

    using Pal2To4Edge = TaggedEdge<NitroTexture.Palette, NitroTexture_Tex4x4.Pal2To4Tag>;
    using Pal2To4Map = BidirectionalGraph<NitroTexture.Palette,
        TaggedEdge<NitroTexture.Palette, NitroTexture_Tex4x4.Pal2To4Tag>>;

    public abstract class NitroTexture
    {
        public uint m_TextureID, m_PaletteID;
        public string m_TextureName, m_PaletteName;

        public int m_Width, m_Height;
        public int m_TexType;
        public byte m_Colour0Mode;
        public uint m_DSTexParam;

        public byte[] m_RawTextureData, m_RawPaletteData;
        public int m_TextureDataLength, m_PaletteDataLength;

        public uint m_EntryOffset, m_PalEntryOffset, m_PalOffset;

        // texture stored as 8bit ARGB
        public byte[] m_ARGB;

        public static NitroTexture ReadFromBMD(BMD bmd, uint texID, uint palID)
        {
            return ReadFromBMD(bmd.m_File, bmd.m_TexChunksOffset, bmd.m_PalChunksOffset, texID, palID);
        }

        protected static NitroTexture ReadFromBMD(NitroFile bmd, uint textureChunkOffset, uint paletteChunkOffset, uint texID, uint palID)
        {
            uint texentry = textureChunkOffset + (texID * 20);
            uint palentry = (palID == 0xFFFFFFFF) ? 0xFFFFFFFF : (paletteChunkOffset + (palID * 16));

            string texname = bmd.ReadString(bmd.Read32(texentry), 0);
            string palname = (palentry == 0xFFFFFFFF) ? "<NO PALETTE>" : bmd.ReadString(bmd.Read32(palentry), 0);

            uint texparam = bmd.Read32(texentry + 0x10);
            int textype = (int)(texparam >> 26) & 0x7;
            if (textype == 0)
                throw new ArgumentException("Texture Type cannot be zero.");

            uint texdataoffset = bmd.Read32(texentry + 0x04);
            uint texdatasize = bmd.Read32(texentry + 0x08);
            byte[] texdata = bmd.ReadBlock(texdataoffset, (textype != 5) ? texdatasize : (texdatasize + (texdatasize / 2)));

            uint paldataoffset = 0xFFFFFFFF;
            uint paldatasize = 0;
            if (palentry != 0xFFFFFFFF)
            {
                paldataoffset = bmd.Read32(palentry + 0x04);
                paldatasize = bmd.Read32(palentry + 0x08);
            }
            byte[] paldata = (paldatasize > 0) ? bmd.ReadBlock(paldataoffset, paldatasize) : null;

            int width = (int)(8 << (int)((texparam >> 20) & 0x7));
            int height = (int)(8 << (int)((texparam >> 23) & 0x7));

            if ((palentry == 0xFFFFFFFF) && (textype != 7))
                throw new ArgumentException("BMD decoder: paletted texture with no associated palette entry; WTF");

            byte colour0Mode = (byte)((texparam & 0x20000000) >> 0x1D);

            NitroTexture texture = FromDataAndType(texID, texname, palID, palname, texdata, paldata, width, height, colour0Mode, textype);

            texture.m_EntryOffset = texentry;
            texture.m_PalEntryOffset = palentry;
            texture.m_PalOffset = paldataoffset;

            return texture;
        }

        public static NitroTexture FromDataAndType(uint texID, string texname, uint palID, string palname,
            byte[] texdata, byte[] paldata, int width, int height, byte colour0Mode, int textype)
        {
            switch (textype)
            {
                case 1:
                    return new NitroTexture_A3I5(texID, texname, palID, palname, texdata, paldata, width, height);
                case 2:
                    return new NitroTexture_Palette4(texID, texname, palID, palname, texdata, paldata, width, height, colour0Mode);
                case 3:
                    return new NitroTexture_Palette16(texID, texname, palID, palname, texdata, paldata, width, height, colour0Mode);
                case 4:
                    return new NitroTexture_Palette256(texID, texname, palID, palname, texdata, paldata, width, height, colour0Mode);
                case 5:
                    return new NitroTexture_Tex4x4(texID, texname, palID, palname, texdata, paldata, width, height);
                case 6:
                    return new NitroTexture_A5I3(texID, texname, palID, palname, texdata, paldata, width, height);
                case 7:
                    return new NitroTexture_Direct(texID, texname, texdata, paldata, width, height);
                default: throw new ArgumentException("Texture type must be 1 - 7");
            }
        }

        public static NitroTexture FromBitmapAndType(uint texID, string texname, uint palID, string palname, Bitmap bmp, 
            int textype)
        {
            switch (textype)
            {
                case 1:
                    return new NitroTexture_A3I5(texID, texname, palID, palname, bmp);
                case 2:
                    return new NitroTexture_Palette4(texID, texname, palID, palname, bmp);
                case 3:
                    return new NitroTexture_Palette16(texID, texname, palID, palname, bmp);
                case 4:
                    return new NitroTexture_Palette256(texID, texname, palID, palname, bmp);
                case 5:
                    return new NitroTexture_Tex4x4(texID, texname, palID, palname, bmp);
                case 6:
                    return new NitroTexture_A5I3(texID, texname, palID, palname, bmp);
                case 7:
                    return new NitroTexture_Direct(texID, texname, bmp);
                default: throw new ArgumentException("Texture type must be 1 - 7");
            }
        }

        protected NitroTexture(uint texID, string texname, uint palID, string palname, byte[] tex, byte[] pal,
            int width, int height, byte colour0Mode, int textype)
        {
            m_TextureID = texID;
            m_TextureName = texname;
            m_PaletteID = palID;
            m_PaletteName = palname;
            m_RawTextureData = tex;
            m_RawPaletteData = pal;
            SetDataSize();
            m_Width = width;
            m_Height = height;
            m_Colour0Mode = colour0Mode;
            m_TexType = textype;
            m_DSTexParam = CalculateTexImageParams();
            m_ARGB = new byte[m_Width * m_Height * 4];

            FromRaw(tex, pal);
        }

        protected NitroTexture(uint texID, string texname, uint palID, string palname, Bitmap bmp, int textype)
        {
            m_TextureID = texID;
            m_TextureName = texname;
            m_PaletteID = palID;
            m_PaletteName = palname;

            int dswidth = 0, dsheight = 0, widthPowerOfTwo = 8, heightPowerOfTwo = 8;
            GetDSWidthAndHeight(bmp.Width, bmp.Height, out dswidth, out dsheight, out widthPowerOfTwo, out heightPowerOfTwo);

            // cheap resizing for textures whose dimensions aren't power-of-two
            if ((widthPowerOfTwo != bmp.Width) || (heightPowerOfTwo != bmp.Height))
            {
                Bitmap newbmp = new Bitmap(widthPowerOfTwo, heightPowerOfTwo);
                Graphics g = Graphics.FromImage(newbmp);
                g.DrawImage(bmp, new Rectangle(0, 0, widthPowerOfTwo, heightPowerOfTwo));
                bmp = newbmp;
            }

            m_Width = bmp.Width;
            m_Height = bmp.Height;
            m_TexType = textype;
            m_Colour0Mode = SetColour0Mode(bmp);
            m_DSTexParam = CalculateTexImageParams();
            m_ARGB = new byte[m_Width * m_Height * 4];

            FromBitmap(bmp);

            SetDataSize();
        }

        public byte[] GetARGB()
        {
            if (m_ARGB.Length == 0)
            {
                FromRaw(m_RawTextureData, m_RawPaletteData);
            }
            return m_ARGB;
        }

        public Bitmap ToBitmap()
        {
            Bitmap bmp = new Bitmap(m_Width, m_Height);

            for (int y = 0; y < m_Height; y++)
            {
                for (int x = 0; x < m_Width; x++)
                {
                    bmp.SetPixel(x, y, Color.FromArgb(m_ARGB[((y * m_Width) + x) * 4 + 3],
                     m_ARGB[((y * m_Width) + x) * 4 + 2],
                     m_ARGB[((y * m_Width) + x) * 4 + 1],
                     m_ARGB[((y * m_Width) + x) * 4]));
                }
            }

            return bmp;
        }

        public static bool BitmapUsesTranslucency(Bitmap bmp)
        {
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    int a = bmp.GetPixel(x, y).A;
                    if (a >= 8 && a <= 248) { return true; }
                }
            }
            return false;
        }

        public static bool BitmapUsesTransparency(Bitmap bmp)
        {
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    int a = bmp.GetPixel(x, y).A;
                    if (a < 8) { return true; }
                }
            }
            return false;
        }

        public static int CountColoursInBitmap(Bitmap bmp)
        {
            HashSet<int> colours = new HashSet<int>();
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    colours.Add(bmp.GetPixel(x, y).ToArgb());
                }
            }
            return colours.Count;
        }

        protected virtual void SetDataSize()
        {
            m_TextureDataLength = m_RawTextureData.Length;
            m_PaletteDataLength = m_RawPaletteData.Length;
        }

        protected virtual byte SetColour0Mode(Bitmap bmp)
        {
            return 0x00;
        }

        protected uint CalculateTexImageParams()
        {
            int dswidth = 0, dsheight = 0, widthPowerOfTwo = 8, heightPowerOfTwo = 8;
            GetDSWidthAndHeight(m_Width, m_Height, out dswidth, out dsheight, out widthPowerOfTwo, out heightPowerOfTwo);
            return GetDSTextureParamsPart1(dswidth, dsheight, m_TexType, m_Colour0Mode);
        }

        protected static void GetDSWidthAndHeight(int texWidth, int texHeight, out int dswidth, out int dsheight,
            out int widthPowerOfTwo, out int heightPowerOfTwo)
        {
            // (for N=0..7: Size=(8 SHL N); ie. 8..1024 texels)
            widthPowerOfTwo = 8; heightPowerOfTwo = 8;
            dswidth = 0; dsheight = 0;
            while (widthPowerOfTwo < texWidth) { widthPowerOfTwo *= 2; dswidth++; }
            while (heightPowerOfTwo < texHeight) { heightPowerOfTwo *= 2; dsheight++; }
        }

        protected static uint GetDSTextureParamsPart1(int dswidth, int dsheight, int textype, byte color0mode)
        {
            uint dstp = (uint)((dswidth << 20) | (dsheight << 23) |
                    (textype << 26) | (color0mode << 29));
            return dstp;
        }

        protected static ushort[] ByteArrayToUShortArray(byte[] bytes)
        {
            ushort[] ushorts = new ushort[bytes.Length / 2];
            for (int i = 0; i < ushorts.Length; i += 2)
            {
                ushorts[i] = (ushort)(bytes[i] | (bytes[i + 1] << 8));
            }
            return ushorts;
        }

        public abstract void FromRaw(byte[] tex, byte[] pal);
        protected abstract void FromBitmap(Bitmap bmp);

        public override bool Equals(Object obj)
        {
            var tx = obj as NitroTexture;
            if (tx == null) return false;
            if (m_TextureID != tx.m_TextureID || m_PaletteID != tx.m_PaletteID) return false;
            if ((m_TextureName == null) != (tx.m_TextureName == null)) return false;
            if (m_TextureName != null && !m_TextureName.Equals(tx.m_TextureName)) return false;
            if ((m_PaletteName == null) != (tx.m_PaletteName == null)) return false;
            if (m_PaletteName != null && !m_PaletteName.Equals(tx.m_PaletteName)) return false;
            if (m_Width != tx.m_Width || m_Height != tx.m_Height) return false;
            if (m_TexType != tx.m_TexType || m_Colour0Mode != tx.m_Colour0Mode || m_DSTexParam != tx.m_DSTexParam) return false;
            if ((m_RawTextureData == null) != (tx.m_RawTextureData == null)) return false;
            if (m_RawTextureData != null && !m_RawTextureData.SequenceEqual(tx.m_RawTextureData)) return false;
            if ((m_RawPaletteData == null) != (tx.m_RawPaletteData == null)) return false;
            if (m_RawPaletteData != null && !m_RawPaletteData.SequenceEqual(tx.m_RawPaletteData)) return false;
            if (m_TextureDataLength != tx.m_TextureDataLength || m_PaletteDataLength != tx.m_PaletteDataLength) return false;
            if ((m_ARGB == null) != (tx.m_ARGB == null)) return false;
            if (m_ARGB != null && !m_ARGB.SequenceEqual(tx.m_ARGB)) return false;
            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 13;
                hash = hash * 7 + m_TextureID.GetHashCode();
                hash = hash * 7 + m_PaletteID.GetHashCode();
                hash = hash * 7 + ((m_TextureName != null) ? m_TextureName.GetHashCode() : -1);
                hash = hash * 7 + ((m_PaletteName != null) ? m_PaletteName.GetHashCode() : -1);
                hash = hash * 7 + m_Width.GetHashCode();
                hash = hash * 7 + m_Height.GetHashCode();
                hash = hash * 7 + m_TexType.GetHashCode();
                hash = hash * 7 + m_Colour0Mode.GetHashCode();
                hash = hash * 7 + m_DSTexParam.GetHashCode();
                hash = hash * 7 + ((m_RawTextureData != null) ? m_RawTextureData.GetHashCode() : -1);
                hash = hash * 7 + ((m_RawPaletteData != null) ? m_RawPaletteData.GetHashCode() : -1);
                hash = hash * 7 + m_TextureDataLength.GetHashCode();
                hash = hash * 7 + m_PaletteDataLength.GetHashCode();
                hash = hash * 7 + ((m_ARGB != null) ? m_ARGB.GetHashCode() : -1);
                return hash;
            }
        }

        public class Palette
        {
            private static int ColorComparer(ushort c1, ushort c2)
            {
                int r1 = c1 & 0x1F;
                int g1 = (c1 >> 5) & 0x1F;
                int b1 = (c1 >> 10) & 0x1F;
                int r2 = c2 & 0x1F;
                int g2 = (c2 >> 5) & 0x1F;
                int b2 = (c2 >> 10) & 0x1F;

                int tdiff = (r2 - r1) + (g2 - g1) + (b2 - b1);
                if (tdiff == 0)
                    return 0;
                else if (tdiff < 0)
                    return 1;
                else
                    return -1;
            }

            public Palette(Palette pal)
            {
                m_Palette = pal.m_Palette.ToList();
                m_FirstColourTransparent = pal.m_FirstColourTransparent;
            
            }

            public Palette(Bitmap bmp, Rectangle region, int depth, bool firstColourTransparent = false)
            {
                List<ushort> pal = new List<ushort>(depth);
                m_FirstColourTransparent = firstColourTransparent;
                if (m_FirstColourTransparent) depth--;

                // 1. get the colors used within the requested region
                for (int y = region.Top; y < region.Bottom; y++)
                {
                    for (int x = region.Left; x < region.Right; x++)
                    {
                        ushort col15 = Helper.ColorToBGR15(bmp.GetPixel(x, y));
                        if (!pal.Contains(col15))
                            pal.Add(col15);
                    }
                }

                // 2. shrink down the palette by removing colors that
                // are close to others, until it fits within the
                // requested size
                pal.Sort(Palette.ColorComparer);
                int maxdiff = 0;
                while (pal.Count > depth)
                {
                    for (int i = 1; i < pal.Count; )
                    {
                        ushort c1 = pal[i - 1];
                        ushort c2 = pal[i];

                        int r1 = c1 & 0x1F;
                        int g1 = (c1 >> 5) & 0x1F;
                        int b1 = (c1 >> 10) & 0x1F;
                        int r2 = c2 & 0x1F;
                        int g2 = (c2 >> 5) & 0x1F;
                        int b2 = (c2 >> 10) & 0x1F;

                        if (Math.Abs(r1 - r2) <= maxdiff && Math.Abs(g1 - g2) <= maxdiff && Math.Abs(b1 - b2) <= maxdiff)
                        {
                            ushort cmerged = Helper.BlendColorsBGR15(c1, 1, c2, 1);
                            pal[i - 1] = cmerged;
                            pal.RemoveAt(i);
                        }
                        else
                            i++;
                    }

                    maxdiff++;
                }

                if (m_FirstColourTransparent) pal.Insert(0, 0x0000);

                m_Palette = pal;
            }

            public int GetBestColorMode(bool transp)
            {
                if (m_Palette.Count <= 2)
                    return transp ? 1 : 3;

                int[][] rgbs = Array.ConvertAll(m_Palette.ToArray(), col => new int[] { col & 0x1f, col >> 5 & 0x1f, col >> 10 & 0x1f });

                if (!transp)
                {
                    for (int i = 0; i < m_Palette.Count; ++i)
                        for (int j = 0; j < m_Palette.Count - 1; ++j)
                            for (int k = 0; k < m_Palette.Count - 2; ++k)
                            {
                                //get a permutation
                                int iR = i, jR = j, kR = k, lR = 0;
                                if (jR >= iR) ++jR;
                                if (kR >= Math.Min(iR, jR)) ++kR;
                                if (kR >= Math.Max(iR, jR)) ++kR;
                                int[] ijkInOrder = new int[] { iR, jR, kR }; Array.Sort(ijkInOrder);
                                if (lR >= ijkInOrder[0]) ++lR;
                                if (lR >= ijkInOrder[1]) ++lR;
                                if (lR >= ijkInOrder[2]) ++lR;

                                bool canUseColMode3_a = true;
                                bool canUseColMode3_b = true; //this is to help check both (3x+5y)/8 and (5x+3y)/8 with a 3-color pallete.
                                for (int comp = 0; comp < 3; ++comp)
                                {
                                    if (rgbs[kR][comp] != (rgbs[iR][comp] * 5 + rgbs[jR][comp] * 3) / 8 || m_Palette.Count == 4 &&
                                        rgbs[lR][comp] != (rgbs[iR][comp] * 3 + rgbs[jR][comp] * 5) / 8)
                                        canUseColMode3_a = false;
                                    if (m_Palette.Count != 3 || rgbs[kR][comp] != (rgbs[iR][comp] * 3 + rgbs[jR][comp] * 5) / 8)
                                        canUseColMode3_b = false;
                                }
                                    

                                if (canUseColMode3_a || canUseColMode3_b)
                                {
                                    if(lR < kR)
                                    {
                                        int temp = kR;
                                        kR = lR;
                                        lR = temp;
                                    }
                                    if (m_Palette.Count == 4)
                                        m_Palette.RemoveAt(lR);
                                    m_Palette.RemoveAt(kR);
                                    return 3;
                                }
                            }
                }
                if (m_Palette.Count == 4)
                    return 2;

                for (int i = 0; i < 3; ++i)
                    for (int j = 0; j < 2; ++j)
                    {
                        int iR = i, jR = j, kR = 0;
                        if (jR >= iR) ++jR;
                        if (kR >= Math.Min(iR, jR)) ++kR;
                        if (kR >= Math.Max(iR, jR)) ++kR;

                        bool canUseColMode1 = true;
                        for (int comp = 0; comp < 3; ++comp)
                            if (rgbs[kR][comp] != (rgbs[iR][comp] + rgbs[jR][comp]) / 2)
                                canUseColMode1 = false;

                        if(canUseColMode1)
                        {
                            m_Palette.RemoveAt(kR);
                            return 1;
                        }
                    }

                return transp ? 0 : 2;
            }

            public int FindClosestColorID(ushort c, int color_mode = -1)
            {
                int r = c & 0x1F;
                int g = (c >> 5) & 0x1F;
                int b = (c >> 10) & 0x1F;

                int maxdiff = 0;

                int startIndex = (m_FirstColourTransparent) ? 1 : 0;
                if(m_Palette.Count > 1 && (color_mode == 1 || color_mode == 3))
                {
                    ushort r0 = (ushort)(m_Palette[0] >>  0 & 0x1f);
                    ushort g0 = (ushort)(m_Palette[0] >>  5 & 0x1f);
                    ushort b0 = (ushort)(m_Palette[0] >> 10 & 0x1f);
 
                    ushort r1 = (ushort)(m_Palette[1] >>  0 & 0x1f);
                    ushort g1 = (ushort)(m_Palette[1] >>  5 & 0x1f);
                    ushort b1 = (ushort)(m_Palette[1] >> 10 & 0x1f);

                    if (color_mode == 1)
                    {
                        m_Palette.Add((ushort)((r0 + r1) / 2 << 0 |
                                               (g0 + g1) / 2 << 5 |
                                               (b0 + b1) / 2 << 10));
                    }
                    if (color_mode == 3)
                    {
                        m_Palette.Add((ushort)((r0 * 5 + r1 * 3) / 8 << 0 |
                                               (g0 * 5 + g1 * 3) / 8 << 5 |
                                               (b0 * 5 + b1 * 3) / 8 << 10));
                        m_Palette.Add((ushort)((r0 * 3 + r1 * 5) / 8 << 0 |
                                               (g0 * 3 + g1 * 5) / 8 << 5 |
                                               (b0 * 3 + b1 * 5) / 8 << 10));
                    }
                }
                for (; ; )
                {
                    for (int i = startIndex; i < m_Palette.Count; i++)
                    {
                        ushort c1 = m_Palette[i];
                        int r1 = c1 & 0x1F;
                        int g1 = (c1 >> 5) & 0x1F;
                        int b1 = (c1 >> 10) & 0x1F;

                        if (Math.Abs(r1 - r) <= maxdiff && Math.Abs(g1 - g) <= maxdiff && Math.Abs(b1 - b) <= maxdiff)
                        {
                            if(m_Palette.Count > 1)
                            {
                                if (color_mode == 1)
                                    m_Palette.RemoveAt(2);
                                if (color_mode == 3)
                                    m_Palette.RemoveRange(2, 2);
                            }
                            
                            return i;
                        }
                    }

                    maxdiff++;
                }
            }

            public byte[] WriteToBytes(int minLength)
            {
                int length = ((m_Palette.Count + 3) & ~3) * 2;
                if (length < minLength) length = minLength;

                byte[] pal = new byte[length];
                for (int i = 0; i < m_Palette.Count; i++)
                {
                    pal[i * 2] = (byte)(m_Palette[i] & 0xFF);
                    pal[(i * 2) + 1] = (byte)(m_Palette[i] >> 8);
                }
                return pal;
            }

            public static bool AreSimilar(Palette p1, Palette p2, out uint offset)
            {
                offset = 0;
                if (p1.m_Palette.Count > p2.m_Palette.Count)
                    return false;

                bool[] mapped = new bool[p1.m_Palette.Count];

                for (int i = 0; i < p1.m_Palette.Count; i++)
                {
                    for(int j = 0; j < p1.m_Palette.Count; ++j)
                    {
                        ushort c1 = p1.m_Palette[i];
                        ushort c2 = p2.m_Palette[j + (int)offset];

                        if(c1 == c2)
                        {
                            mapped[i] = true;
                            break;
                        }

                        if (p1.m_Palette.Count == 2 && p2.m_Palette.Count == 4 && offset == 0)
                        {
                            offset = 2;
                            j = -1;
                        }
                    }
                }

                return Array.TrueForAll(mapped, b => b);
            }

            public List<ushort> m_Palette;
            private bool m_FirstColourTransparent;
        }

        protected class PaletteEqualityComparer : IEqualityComparer<Palette>
        {
            public bool Equals(Palette x, Palette y) { uint dummy;  return Palette.AreSimilar(x, y, out dummy); }
            public  int GetHashCode(Palette x) {
                return x.m_Palette[0] ^
                       (x.m_Palette.Count > 1 ? x.m_Palette[1] << 15 : 0) ^
                       (x.m_Palette.Count > 2 ? x.m_Palette[2] << 17 : 0) ^
                       (x.m_Palette.Count > 3 ? x.m_Palette[3] << 2 : 0); }
        }
    }

    public class NitroTexture_A3I5 : NitroTexture
    {
        public NitroTexture_A3I5(uint texID, string texname, uint palID, string palname, byte[] tex, byte[] pal,
            int width, int height)
            : base(texID, texname, palID, palname, tex, pal, width, height, 0, 1) { }

        public NitroTexture_A3I5(uint texID, string texname, uint palID, string palname, Bitmap bmp)
            : base(texID, texname, palID, palname, bmp, 1) { }

        public override void FromRaw(byte[] tex, byte[] pal)
        {
            for (uint _in = 0, _out = 0; _in < m_TextureDataLength; _in++, _out += 4)
            {
                byte texel = tex[_in];
                ushort color = Helper.BytesToUShort16(pal, ((texel & 0x1F) << 1));

                byte red = (byte)((color & 0x001F) << 3);
                byte green = (byte)((color & 0x03E0) >> 2);
                byte blue = (byte)((color & 0x7C00) >> 7);
                byte _alpha = (byte)(((texel & 0xE0) >> 3) + ((texel & 0xE0) >> 6));
                byte alpha = (byte)((_alpha << 3) | (_alpha >> 2));

                m_ARGB[_out] = blue;
                m_ARGB[_out + 1] = green;
                m_ARGB[_out + 2] = red;
                m_ARGB[_out + 3] = alpha;
            }
        }

        protected override void FromBitmap(Bitmap bmp)
        {
            m_RawTextureData = new byte[bmp.Width * bmp.Height];
            Palette _pal = new Palette(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height), 32);
            int alphamask = 0xE0;

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    ushort bgr15 = Helper.ColorToBGR15(c);
                    int a = c.A & alphamask;

                    byte val = (byte)(_pal.FindClosestColorID(bgr15) | a);
                    m_RawTextureData[(y * bmp.Width) + x] = val;
                }
            }

            m_RawPaletteData = _pal.WriteToBytes(16);
        }
    }

    public class NitroTexture_Palette4 : NitroTexture
    {
        public NitroTexture_Palette4(uint texID, string texname, uint palID, string palname, byte[] tex, byte[] pal,
            int width, int height, byte colour0Mode)
            : base(texID, texname, palID, palname, tex, pal, width, height, colour0Mode, 2) { }

        public NitroTexture_Palette4(uint texID, string texname, uint palID, string palname, Bitmap bmp)
            : base(texID, texname, palID, palname, bmp, 2) { }

        public override void FromRaw(byte[] tex, byte[] pal)
        {
            byte zeroAlpha = (byte)((m_Colour0Mode > 0) ? 0x00 : 0xFF);
            for (int _in = 0, _out = 0; _in < m_TextureDataLength; _in++, _out += 16)
            {
                byte texels = tex[_in];

                ushort color = Helper.BytesToUShort16(pal, ((texels << 1) & 0x6));
                byte red = (byte)((color & 0x001F) << 3);
                byte green = (byte)((color & 0x03E0) >> 2);
                byte blue = (byte)((color & 0x7C00) >> 7);

                m_ARGB[_out] = blue;
                m_ARGB[_out + 1] = green;
                m_ARGB[_out + 2] = red;
                m_ARGB[_out + 3] = (byte)(((texels & 0x03) != 0) ? (byte)0xFF : zeroAlpha);

                color = Helper.BytesToUShort16(pal, ((texels >> 1) & 0x6));
                red = (byte)((color & 0x001F) << 3);
                green = (byte)((color & 0x03E0) >> 2);
                blue = (byte)((color & 0x7C00) >> 7);

                m_ARGB[_out + 4] = blue;
                m_ARGB[_out + 5] = green;
                m_ARGB[_out + 6] = red;
                m_ARGB[_out + 7] = (byte)(((texels & 0x0C) != 0) ? (byte)0xFF : zeroAlpha);

                color = Helper.BytesToUShort16(pal, ((texels >> 3) & 0x6));
                red = (byte)((color & 0x001F) << 3);
                green = (byte)((color & 0x03E0) >> 2);
                blue = (byte)((color & 0x7C00) >> 7);

                m_ARGB[_out + 8] = blue;
                m_ARGB[_out + 9] = green;
                m_ARGB[_out + 10] = red;
                m_ARGB[_out + 11] = (byte)(((texels & 0x30) != 0) ? (byte)0xFF : zeroAlpha);

                color = Helper.BytesToUShort16(pal, ((texels >> 5) & 0x6));
                red = (byte)((color & 0x001F) << 3);
                green = (byte)((color & 0x03E0) >> 2);
                blue = (byte)((color & 0x7C00) >> 7);

                m_ARGB[_out + 12] = blue;
                m_ARGB[_out + 13] = green;
                m_ARGB[_out + 14] = red;
                m_ARGB[_out + 15] = (byte)(((texels & 0xC0) != 0) ? (byte)0xFF : zeroAlpha);
            }
        }

        protected override void FromBitmap(Bitmap bmp)
        {
            m_RawTextureData = new byte[(bmp.Width * bmp.Height) / 4];

            Palette txpal = new Palette(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height), 4);

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0, _out = 0; x < bmp.Width; x += 4, _out++)
                {
                    for (int t = 0; t < 4; t++)
                    {
                        Color c = bmp.GetPixel(x + t, y);
                        ushort bgr15 = Helper.ColorToBGR15(c);

                        byte val = (byte)((c.A < 8) ? 0 : txpal.FindClosestColorID(bgr15));
                        m_RawTextureData[(y * (bmp.Width / 4)) + _out] |= (byte)((val & 0x03) << (t * 2));
                    }
                }
            }

            m_RawPaletteData = txpal.WriteToBytes(8);
        }

        protected override byte SetColour0Mode(Bitmap bmp)
        {
            return (byte)((BitmapUsesTransparency(bmp)) ? 0x01 : 0x00);
        }
    }

    public class NitroTexture_Palette16 : NitroTexture
    {
        public NitroTexture_Palette16(uint texID, string texname, uint palID, string palname, byte[] tex, byte[] pal,
            int width, int height, byte colour0Mode)
            : base(texID, texname, palID, palname, tex, pal, width, height, colour0Mode, 3) { }

        public NitroTexture_Palette16(uint texID, string texname, uint palID, string palname, Bitmap bmp)
            : base(texID, texname, palID, palname, bmp, 3) { }

        public override void FromRaw(byte[] tex, byte[] pal)
        {
            byte zeroAlpha = (byte)((m_Colour0Mode > 0) ? 0x00 : 0xFF);
            for (int _in = 0, _out = 0; _in < m_TextureDataLength; _in++, _out += 8)
            {
                byte texels = tex[_in];

                ushort color = Helper.BytesToUShort16(pal, ((texels << 1) & 0x1E));
                byte red = (byte)((color & 0x001F) << 3);
                byte green = (byte)((color & 0x03E0) >> 2);
                byte blue = (byte)((color & 0x7C00) >> 7);

                m_ARGB[_out] = blue;
                m_ARGB[_out + 1] = green;
                m_ARGB[_out + 2] = red;
                m_ARGB[_out + 3] = (byte)(((texels & 0x0F) != 0) ? (byte)0xFF : zeroAlpha);

                color = Helper.BytesToUShort16(pal, ((texels >> 3) & 0x1E));
                red = (byte)((color & 0x001F) << 3);
                green = (byte)((color & 0x03E0) >> 2);
                blue = (byte)((color & 0x7C00) >> 7);

                m_ARGB[_out + 4] = blue;
                m_ARGB[_out + 5] = green;
                m_ARGB[_out + 6] = red;
                m_ARGB[_out + 7] = (byte)(((texels & 0xF0) != 0) ? (byte)0xFF : zeroAlpha);
            }
        }

        protected override void FromBitmap(Bitmap bmp)
        {
            m_RawTextureData = new byte[(bmp.Width * bmp.Height) / 2];

            Palette txpal = new Palette(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height), 16);

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0, _out = 0; x < bmp.Width; x += 2, _out++)
                {
                    for (int t = 0; t < 2; t++)
                    {
                        Color c = bmp.GetPixel(x + t, y);
                        ushort bgr15 = Helper.ColorToBGR15(c);

                        byte val = (byte)((c.A < 8) ? 0 : txpal.FindClosestColorID(bgr15));
                        m_RawTextureData[(y * (bmp.Width / 2)) + _out] |= (byte)((val & 0x0F) << (t * 4));
                    }
                }
            }

            m_RawPaletteData = txpal.WriteToBytes(8);
        }

        protected override byte SetColour0Mode(Bitmap bmp)
        {
            return (byte)((BitmapUsesTransparency(bmp)) ? 0x01 : 0x00);
        }
    }

    public class NitroTexture_Palette256 : NitroTexture
    {
        public NitroTexture_Palette256(uint texID, string texname, uint palID, string palname, byte[] tex, byte[] pal,
            int width, int height, byte colour0Mode)
            : base(texID, texname, palID, palname, tex, pal, width, height, colour0Mode, 4) { }

        public NitroTexture_Palette256(uint texID, string texname, uint palID, string palname, Bitmap bmp)
            : base(texID, texname, palID, palname, bmp, 4) { }

        public override void FromRaw(byte[] tex, byte[] pal)
        {
            byte zeroAlpha = (byte)((m_Colour0Mode > 0) ? 0x00 : 0xFF);
            for (int _in = 0, _out = 0; _in < m_TextureDataLength; _in++, _out += 4)
            {
                byte texel = tex[_in];

                ushort color = Helper.BytesToUShort16(pal, (texel << 1));
                byte red = (byte)((color & 0x001F) << 3);
                byte green = (byte)((color & 0x03E0) >> 2);
                byte blue = (byte)((color & 0x7C00) >> 7);

                m_ARGB[_out] = blue;
                m_ARGB[_out + 1] = green;
                m_ARGB[_out + 2] = red;
                m_ARGB[_out + 3] = (byte)((texel != 0) ? (byte)0xFF : zeroAlpha);
            }
        }

        protected override void FromBitmap(Bitmap bmp)
        {
            m_RawTextureData = new byte[m_Width * m_Height];

            Palette txpal = new Palette(bmp, new Rectangle(0, 0, m_Width, m_Height), 256);

            for (int y = 0; y < m_Height; y++)
            {
                for (int x = 0; x < m_Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    ushort bgr15 = Helper.ColorToBGR15(c);

                    byte val = (byte)((c.A < 8) ? 0 : txpal.FindClosestColorID(bgr15));
                    m_RawTextureData[(y * m_Width) + x] = val;
                }
            }

            m_RawPaletteData = txpal.WriteToBytes(16);
        }

        protected override byte SetColour0Mode(Bitmap bmp)
        {
            return (byte)((BitmapUsesTransparency(bmp)) ? 0x01 : 0x00);
        }
    }

    public class NitroTexture_Tex4x4 : NitroTexture
    {
        public NitroTexture_Tex4x4(uint texID, string texname, uint palID, string palname, byte[] tex, byte[] pal,
            int width, int height)
            : base(texID, texname, palID, palname, tex, pal, width, height, 0, 5) { }

        public NitroTexture_Tex4x4(uint texID, string texname, uint palID, string palname, Bitmap bmp)
            : base(texID, texname, palID, palname, bmp, 5) { }

        public override void FromRaw(byte[] tex, byte[] pal)
        {
            int yout = 0, xout = 0;

            for (int _in = 0; _in < m_TextureDataLength; _in += 4)
            {
                uint blox = Helper.BytesToUInt32(tex, _in);
                ushort palidx_data = Helper.BytesToUShort16(tex, m_TextureDataLength + (_in >> 1));

                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        byte texel = (byte)(blox & 0x3);
                        blox >>= 2;

                        int pal_offset = (int)((palidx_data & 0x3FFF) << 2);
                        ushort color_mode = (ushort)(palidx_data >> 14);
                        uint color = 0xFFFFFFFF;

                        switch (texel)
                        {
                            case 0: color = Helper.BytesToUShort16(pal, pal_offset, 0); break;
                            case 1: color = Helper.BytesToUShort16(pal, pal_offset + 2, 0); break;
                            case 2:
                                {
                                    switch (color_mode)
                                    {
                                        case 0:
                                        case 2: color = Helper.BytesToUShort16(pal, pal_offset + 4, 0); break;
                                        case 1:
                                            {
                                                ushort c0 = Helper.BytesToUShort16(pal, pal_offset, 0);
                                                ushort c1 = Helper.BytesToUShort16(pal, pal_offset + 2, 0);
                                                color = Helper.BlendColorsBGR15(c0, 1, c1, 1);
                                            }
                                            break;
                                        case 3:
                                            {
                                                ushort c0 = Helper.BytesToUShort16(pal, pal_offset, 0);
                                                ushort c1 = Helper.BytesToUShort16(pal, pal_offset + 2, 0);
                                                color = Helper.BlendColorsBGR15(c0, 5, c1, 3);
                                            }
                                            break;
                                    }
                                }
                                break;
                            case 3:
                                {
                                    switch (color_mode)
                                    {
                                        case 0:
                                        case 1: color = 0xFFFFFFFF; break;
                                        case 2: color = Helper.BytesToUShort16(pal, pal_offset + 6, 0); break;
                                        case 3:
                                            {
                                                ushort c0 = Helper.BytesToUShort16(pal, pal_offset, 0);
                                                ushort c1 = Helper.BytesToUShort16(pal, pal_offset + 2, 0);
                                                color = Helper.BlendColorsBGR15(c0, 3, c1, 5);
                                            }
                                            break;
                                    }
                                }
                                break;
                        }

                        int _out = (int)(((yout * m_Width) + xout) * 4);
                        int yoff = (int)(y * m_Width * 4);
                        int xoff = (int)(x * 4);

                        if (color == 0xFFFFFFFF)
                        {
                            m_ARGB[_out + yoff + xoff] = 0;
                            m_ARGB[_out + yoff + xoff + 1] = 0;
                            m_ARGB[_out + yoff + xoff + 2] = 0;
                            m_ARGB[_out + yoff + xoff + 3] = 0;
                        }
                        else
                        {
                            byte red = (byte)((color & 0x001F) << 3);
                            byte green = (byte)((color & 0x03E0) >> 2);
                            byte blue = (byte)((color & 0x7C00) >> 7);

                            m_ARGB[_out + yoff + xoff] = blue;
                            m_ARGB[_out + yoff + xoff + 1] = green;
                            m_ARGB[_out + yoff + xoff + 2] = red;
                            m_ARGB[_out + yoff + xoff + 3] = 0xFF;
                        }
                    }
                }

                xout += 4;
                if (xout >= m_Width)
                {
                    xout = 0;
                    yout += 4;
                }
            }
        }

        protected override void SetDataSize()
        {
            m_TextureDataLength = m_RawTextureData.Length - (m_RawTextureData.Length / 3);
            m_PaletteDataLength = m_RawPaletteData.Length;
        }

        protected struct Mapping3ColTo4Col
        {
            public int m_OtherIndex;
            public int m_LastColIndex;
            public Mapping3ColTo4Col(int otherID, int lastColID)
            {
                m_OtherIndex = otherID;
                m_LastColIndex = lastColID;
            }
        }

        
        protected void Map3ColPalTo4ColPal(List<Palette> pal3Color, List<Palette> pal4Color)
        {
            Pal3To4Map graph = new Pal3To4Map();
            graph.AddVertexRange(pal3Color);
            graph.AddVertexRange(pal4Color);
            //Generate the mapping graph
            foreach (Palette pal3 in pal3Color)
                foreach (Palette pal4 in pal4Color)
                {
                    int firstNope = pal4.m_Palette.FindIndex(x => !pal3.m_Palette.Contains(x));
                    int lastNope = pal4.m_Palette.FindLastIndex(x => !pal3.m_Palette.Contains(x));
                    if (firstNope == lastNope)
                        graph.AddEdge(new Pal3To4Edge(pal3, pal4, firstNope));
                }

            //remove obvious duplicates
            foreach (Pal3To4Edge edge in graph.Edges.ToList())
            {
                if (graph.Degree(edge.Source) > 1 && graph.Degree(edge.Target) > 1)
                    graph.RemoveEdge(edge);
            }
            //finish the mapping so it's 1-to-1
            foreach (Pal3To4Edge edge in graph.Edges.ToList())
            {
                if (graph.Degree(edge.Source) > 1 || graph.Degree(edge.Target) > 1)
                    graph.RemoveEdge(edge);
            }
            foreach (Palette vert in graph.Vertices.Where(x => x.m_Palette.Count == 3))
            {
                if(graph.Degree(vert) > 0)
                {
                    Pal3To4Edge edge = graph.OutEdge(vert, 0);
                    ushort temp = edge.Target.m_Palette[edge.Tag];
                    edge.Target.m_Palette[edge.Tag] = edge.Target.m_Palette[3];
                    edge.Target.m_Palette[3] = temp;
                }
                else
                {
                    pal4Color.Add(new Palette(vert));
                    pal4Color.Last().m_Palette.Add(0x8000); //a new color: "Color.DONT_CARE"
                }
            }
        }

        public struct Pal2To4Tag
        {
            public int m_3rdColorIndex;
            public int m_Offset;
            public ushort m_LastColor;
        }
        protected void Map2ColPalTo4ColPal(List<Palette> pal2Color, List<Palette> pal4Color)
        {
            Pal2To4Map graph = new Pal2To4Map();
            graph.AddVertexRange(pal2Color);
            graph.AddVertexRange(pal4Color);
            //thirdColorIndex: index to the color that should be the third one. The 4th one cannot be moved.
            //offset: where the pallete is found in the 4-color pallete; 0 or 2.
            //lastColor: the 4th color could be Color.DONT_CARE, so this is what it really should be.
            //           Used only if offset == 2.
            List<int> threeInts = new List<int>{ 0, 1, 2 };
            foreach (Palette pal2 in pal2Color)
                foreach (Palette pal4 in pal4Color)
                {
                    int firstYep = pal4.m_Palette.FindIndex(x => pal2.m_Palette.Contains(x) || x == 0x8000);
                    int lastYep = pal4.m_Palette.FindLastIndex(x => pal2.m_Palette.Contains(x) || x == 0x8000);
                    if(firstYep != lastYep) //which will be false also when they're both -1 (nothing found)
                    {
                        Pal2To4Tag tag;
                        if(lastYep == 3)
                        {
                            tag.m_3rdColorIndex = firstYep;
                            tag.m_Offset = 2;
                            tag.m_LastColor = pal2.m_Palette.Find(x => x != pal4.m_Palette[firstYep]);
                        }
                        else
                        {
                            tag.m_3rdColorIndex = threeInts.Find(x => x != firstYep && x != lastYep);
                            tag.m_Offset = 0;
                            tag.m_LastColor = 0x8000;
                        }
                        graph.AddEdge(new Pal2To4Edge(pal2, pal4, tag));
                    }
                }
            
            foreach (Pal2To4Edge edge in graph.Edges.ToList())
            {
                if (edge.Tag.m_Offset == 2 && graph.Degree(edge.Source) > 1 &&
                    graph.InEdges(edge.Target).ToList().Exists(x => x.Tag.m_Offset == 2 &&
                                                               x.Tag.m_3rdColorIndex == edge.Tag.m_3rdColorIndex &&
                                                               x.Tag.m_LastColor != edge.Tag.m_LastColor))
                {
                    graph.RemoveEdge(edge);
                }
            }
            foreach (Pal2To4Edge edge in graph.Edges.ToList())
            {
                if (edge.Tag.m_Offset == 2 &&
                    graph.InEdges(edge.Target).ToList().Exists(x => x.Tag.m_Offset == 2 &&
                                                               x.Tag.m_3rdColorIndex == edge.Tag.m_3rdColorIndex &&
                                                               x.Tag.m_LastColor != edge.Tag.m_LastColor))
                {
                    graph.RemoveEdge(edge);
                }
            }
            //now each target vertex has only 3 "groups" of edges, where each group has at most
            //an edge with offset 0 and an edge with offset 2.
            foreach (Palette pal4 in graph.Vertices.Where(x => x.m_Palette.Count == 4))
            {
                if (graph.InEdges(pal4).Count() == 0)
                    continue;

                List<IGrouping<int, Pal2To4Edge>> groups =
                    graph.InEdges(pal4).GroupBy(x => x.Tag.m_3rdColorIndex).ToList();
                List<int> groupsToDelete = groups.ConvertAll(x => x.Key);
                List<int> weights = new List<int>();
                foreach (IGrouping<int, Pal2To4Edge> group in groups)
                {
                    List<Pal2To4Edge> elemList = group.ToList();
                    int weight = elemList.Count(x => graph.Degree(x.Source) == 1) * 2; //how much do we NOT want to delete the group
                    if (elemList.Count > 1)
                        weight += 1; //bigger group is obviously heavier.
                    if(elemList.Count > 1 &&
                        graph.Degree(elemList[0].Source) == 2 &&
                        graph.Degree(elemList[1].Source) == 2)
                    {
                        Pal2To4Edge edge0 = graph.OutEdges(elemList[0].Source).ToList().Find(x => x != elemList[0]);
                        Pal2To4Edge edge1 = graph.OutEdges(elemList[1].Source).ToList().Find(x => x != elemList[1]);
                        if (edge0.Source == edge1.Source && (edge0.Tag.m_3rdColorIndex != edge1.Tag.m_3rdColorIndex ||
                            edge0.Tag.m_Offset == edge1.Tag.m_Offset))
                            weight += 2;
                    }
                    weights.Add(weight);
                }

                int safeIndex = weights.IndexOf(weights.Max());
                groupsToDelete.RemoveAt(safeIndex);
                foreach (int groupKey in groupsToDelete)
                {
                    List<Pal2To4Edge> edges = graph.InEdges(pal4).Where(x => x.Tag.m_3rdColorIndex == groupKey).ToList();
                    for(int i = edges.Count() - 1; i >= 0; --i)
                        graph.RemoveEdge(edges[i]);
                }
            }
            
            //finish the mapping so it's many-to-1
            foreach (Pal2To4Edge edge in graph.Edges.ToList())
            {
                if (graph.Degree(edge.Source) > 1)
                    graph.RemoveEdge(edge);
            }

            foreach (Palette vert in graph.Vertices.Where(x => x.m_Palette.Count == 2))
            {
                if (graph.Degree(vert) > 0)
                {
                    Pal2To4Edge edge = graph.OutEdge(vert, 0);
                    ushort temp = edge.Target.m_Palette[edge.Tag.m_3rdColorIndex];
                    edge.Target.m_Palette[edge.Tag.m_3rdColorIndex] = edge.Target.m_Palette[2];
                    edge.Target.m_Palette[2] = temp;
                    if (edge.Tag.m_Offset == 2)
                        edge.Target.m_Palette[3] = edge.Tag.m_LastColor; //they would equal already if that was a 4-color palette.

                    if(graph.Degree(edge.Target) > 1)
                    {
                        Pal2To4Edge otherEdge = graph.InEdges(edge.Target).ToList().Find(x => x.Source != edge.Source);
                        Pal2To4Tag tag = otherEdge.Tag;
                        tag.m_3rdColorIndex = 2; //Avoid the double swap when two 2-color palettes can both fit in a 4-color palette.
                        otherEdge.Tag = tag;
                    }
                }
                else
                {
                    if (pal4Color.Count == 0 || pal4Color.Last().m_Palette.Count == 0)
                        pal4Color.Add(new Palette(vert));
                    else
                        pal4Color.Last().m_Palette.AddRange(vert.m_Palette);
                }
            }
        }
        protected override void FromBitmap(Bitmap bmp)
        {
            m_RawTextureData = new byte[((m_Width * m_Height) / 16) * 6];
            Palette[] pallist = new Palette[(m_Height / 4) * (m_Width / 4)];
            int[] colorModes = new int[(m_Height / 4) * (m_Width / 4)];
            List<ushort> paldata = new List<ushort>();

            List<Palette> pal4Color = new List<Palette>();
            List<Palette> pal3Color = new List<Palette>();
            List<Palette> pal2Color = new List<Palette>();
            List<Palette> pal1Color = new List<Palette>();

            int texoffset = 0;
            int palidxoffset = ((m_Width * m_Height) / 16) * 4;

            for (int y = 0; y < m_Height; y += 4)
            {
                for (int x = 0; x < m_Width; x += 4)
                {
                    bool transp = false;

                    for (int y2 = 0; y2 < 4; y2++)
                    {
                        for (int x2 = 0; x2 < 4; x2++)
                        {
                            Color c = bmp.GetPixel(x + x2, y + y2);

                            if (c.A < 8)
                                transp = true;
                        }
                    }

                    Palette txpal = new Palette(bmp, new Rectangle(x, y, 4, 4), transp ? 3 : 4);
                    colorModes[(y / 4) * (m_Width / 4) + (x / 4)] = txpal.GetBestColorMode(transp);
                    int size = txpal.m_Palette.Count;
                    List<Palette> correctList = 
                        new List<Palette>[]{pal1Color, pal2Color, pal3Color, pal4Color}[size - 1];
                    correctList.Add(txpal);
                    pallist[(y / 4) * (m_Width / 4) + (x / 4)] = new Palette(txpal);
                }
            }
            pal4Color = new HashSet<Palette>(pal4Color, new PaletteEqualityComparer()).ToList(); //remove the duplicates
            pal3Color = new HashSet<Palette>(pal3Color, new PaletteEqualityComparer()).ToList();
            pal2Color = new HashSet<Palette>(pal2Color, new PaletteEqualityComparer()).ToList();
            Map3ColPalTo4ColPal(pal3Color, pal4Color);
            Map2ColPalTo4ColPal(pal2Color, pal4Color);
            foreach (Palette pal in pal4Color)
                paldata.AddRange(pal.m_Palette);
            foreach (Palette pal in pal1Color)
            {
                int index = paldata.FindIndex(x => x == pal.m_Palette[0] || x == 0x8000);
                if (index != -1)
                    paldata[index] = pal.m_Palette[0];
                else
                    paldata.Add(pal.m_Palette[0]);
            }

            if (paldata.Count % 2 == 1)
                paldata.Add(0x8000); //just in case a single color palette got placed at the end

            for (int y = 0; y < m_Height / 4; ++y)
            {
                for (int x = 0; x < m_Width / 4; ++x)
                {

                    Palette txpal = pallist[y * (m_Width / 4) + x];
                    uint texel = 0;
                    int color_mode = colorModes[y * (m_Width / 4) + x];
                    ushort palidx = (ushort)(color_mode << 14);
                    int realPalSize = txpal.m_Palette.Count;
                    if (txpal.m_Palette.Count == 1 || txpal.m_Palette.Count == 3 && color_mode == 2)
                        txpal.m_Palette.Add(0x8000);

                    int index = -1;
                    for(int i = 0; i < paldata.Count - (txpal.m_Palette.Count - 1); i += 2)
                    {
                        index = paldata.GetRange(i, txpal.m_Palette.Count).Count(p =>
                            txpal.m_Palette.Exists(q => q == p) && p != 0x8000) >= realPalSize ? i : -1;
                        if (index != -1)
                            break;
                    }
                    txpal.m_Palette = paldata.GetRange(index, txpal.m_Palette.Count);
                    palidx |= (ushort)(index / 2);

                    for (int y2 = 0; y2 < 4; y2++)
                    {
                        for (int x2 = 0; x2 < 4; x2++)
                        {
                            int px = 0;
                            Color c = bmp.GetPixel(4 * x + x2, 4 * y + y2);
                            ushort bgr15 = Helper.ColorToBGR15(c);

                            if (color_mode < 2 && c.A < 8)
                                px = 3;
                            else
                                px = txpal.FindClosestColorID(bgr15, color_mode);

                            texel |= (uint)(px << ((2 * x2) + (8 * y2)));
                        }
                    }

                    m_RawTextureData[texoffset] = (byte)(texel & 0xFF);
                    m_RawTextureData[texoffset + 1] = (byte)((texel >> 8) & 0xFF);
                    m_RawTextureData[texoffset + 2] = (byte)((texel >> 16) & 0xFF);
                    m_RawTextureData[texoffset + 3] = (byte)(texel >> 24);
                    texoffset += 4;
                    m_RawTextureData[palidxoffset] = (byte)(palidx & 0xFF);
                    m_RawTextureData[palidxoffset + 1] = (byte)(palidx >> 8);
                    palidxoffset += 2;
                }
            }

            m_RawPaletteData = new byte[((paldata.Count + 3) & ~3) * 2];
            if (m_RawPaletteData.Length < 16) Array.Resize(ref m_RawPaletteData, 16);
            for (int i = 0; i < paldata.Count; i++)
            {
                m_RawPaletteData[i * 2] = (byte)(paldata[i] & 0xFF);
                m_RawPaletteData[(i * 2) + 1] = (byte)(paldata[i] >> 8);
            }
        }
    }

    public class NitroTexture_A5I3 : NitroTexture
    {
        public NitroTexture_A5I3(uint texID, string texname, uint palID, string palname, byte[] tex, byte[] pal,
            int width, int height)
            : base(texID, texname, palID, palname, tex, pal, width, height, 0, 6) { }

        public NitroTexture_A5I3(uint texID, string texname, uint palID, string palname, Bitmap bmp)
            : base(texID, texname, palID, palname, bmp, 6) { }

        public override void FromRaw(byte[] tex, byte[] pal)
        {
            for (int _in = 0, _out = 0; _in < m_TextureDataLength; _in++, _out += 4)
            {
                byte texel = tex[_in];
                ushort color = Helper.BytesToUShort16(pal, ((texel & 0x07) << 1));

                byte red = (byte)((color & 0x001F) << 3);
                byte green = (byte)((color & 0x03E0) >> 2);
                byte blue = (byte)((color & 0x7C00) >> 7);
                byte alpha = (byte)((texel & 0xF8) | ((texel & 0xF8) >> 5));

                m_ARGB[_out] = blue;
                m_ARGB[_out + 1] = green;
                m_ARGB[_out + 2] = red;
                m_ARGB[_out + 3] = alpha;
            }
        }

        protected override void FromBitmap(Bitmap bmp)
        {
            m_RawTextureData = new byte[bmp.Width * bmp.Height];
            Palette _pal = new Palette(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height), 32);
            int alphamask = 0xF8;

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    ushort bgr15 = Helper.ColorToBGR15(c);
                    int a = c.A & alphamask;

                    byte val = (byte)(_pal.FindClosestColorID(bgr15) | a);
                    m_RawTextureData[(y * bmp.Width) + x] = val;
                }
            }

            m_RawPaletteData = _pal.WriteToBytes(16);
        }
    }

    public class NitroTexture_Direct : NitroTexture
    {
        public NitroTexture_Direct(uint texID, string texname, byte[] tex, byte[] pal,
            int width, int height)
            : base(texID, texname, 0xFFFFFFFF, null, tex, pal, width, height, 0, 7) { }

        public NitroTexture_Direct(uint texID, string texname, Bitmap bmp)
            : base(texID, texname, 0xFFFFFFFF, null, bmp, 7) { }

        public override void FromRaw(byte[] tex, byte[] pal)
        {
            for (int _in = 0, _out = 0; _in < m_TextureDataLength; _in += 2, _out += 4)
            {
                ushort color = Helper.BytesToUShort16(tex, _in);
                byte red = (byte)((color & 0x001F) << 3);
                byte green = (byte)((color & 0x03E0) >> 2);
                byte blue = (byte)((color & 0x7C00) >> 7);

                m_ARGB[_out] = blue;
                m_ARGB[_out + 1] = green;
                m_ARGB[_out + 2] = red;
                m_ARGB[_out + 3] = (byte)(((color & 0x8000) != 0) ? (byte)0xFF : 0x00);
            }
        }

        protected override void SetDataSize()
        {
            m_TextureDataLength = m_RawTextureData.Length;
            m_PaletteDataLength = 0;
        }

        protected override void FromBitmap(Bitmap bmp)
        {
            m_RawTextureData = new byte[(m_Width * m_Height) * 2];

            for (int y = 0; y < m_Height; y++)
            {
                for (int x = 0; x < m_Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    ushort bgr15 = Helper.ColorToBGR15(c);
                    if (c.A > 0) bgr15 |= 0x8000;

                    m_RawTextureData[(((y * m_Width) + x) * 2)] = (byte)(bgr15 & 0xFF);
                    m_RawTextureData[(((y * m_Width) + x) * 2) + 1] = (byte)(bgr15 >> 8);
                }
            }
        }
    }
}
