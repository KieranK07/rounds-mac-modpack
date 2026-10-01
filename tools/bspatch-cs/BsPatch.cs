// Applies the repo's bsdiff patches (BSDIFF40, bzip2 blocks) on Windows, where there's no bspatch and .NET has no
// bzip2. install-windows.ps1 compiles this with Add-Type, so it sticks to C# 5 (Windows PowerShell 5.1's compiler).
// Every result is checked against the SHA-256 in manifest/patches.tsv, so a decoding bug can't slip through.
using System;
using System.IO;
using System.Text;

namespace RoundsModpack
{
    public static class BsPatch
    {
        public static void Apply(string oldFile, string patchFile, string newFile)
        {
            byte[] old = File.ReadAllBytes(oldFile), p = File.ReadAllBytes(patchFile);
            if (p.Length < 32 || Encoding.ASCII.GetString(p, 0, 8) != "BSDIFF40") throw new InvalidDataException("not a bsdiff patch: " + patchFile);
            long ctrlLen = OffT(p, 8), diffLen = OffT(p, 16), newSize = OffT(p, 24);
            if (ctrlLen < 0 || diffLen < 0 || newSize < 0 || 32 + ctrlLen + diffLen > p.Length) throw new InvalidDataException("corrupt patch header");
            byte[] ctrl = Bzip2.Decompress(p, 32, (int)ctrlLen);
            byte[] diff = Bzip2.Decompress(p, 32 + (int)ctrlLen, (int)diffLen);
            byte[] extra = Bzip2.Decompress(p, 32 + (int)(ctrlLen + diffLen), p.Length - 32 - (int)(ctrlLen + diffLen));

            byte[] result = new byte[newSize];
            long oldPos = 0, newPos = 0;
            int c = 0, d = 0, e = 0;
            while (newPos < newSize)
            {
                if (c + 24 > ctrl.Length) throw new InvalidDataException("corrupt patch (control)");
                long x = OffT(ctrl, c), y = OffT(ctrl, c + 8), z = OffT(ctrl, c + 16);
                c += 24;
                if (x < 0 || y < 0 || newPos + x + y > newSize || d + x > diff.Length || e + y > extra.Length)
                    throw new InvalidDataException("corrupt patch (lengths)");
                for (long i = 0; i < x; i++)
                {
                    byte b = diff[d++];
                    long o = oldPos + i;
                    if (o >= 0 && o < old.Length) b = (byte)(b + old[o]);
                    result[newPos + i] = b;
                }
                newPos += x; oldPos += x;
                Buffer.BlockCopy(extra, e, result, (int)newPos, (int)y);
                e += (int)y;
                newPos += y; oldPos += z;
            }
            File.WriteAllBytes(newFile, result);
        }

        // bsdiff's 8-byte integers: little-endian magnitude, sign in the top bit.
        static long OffT(byte[] b, int i)
        {
            long y = b[i + 7] & 0x7F;
            for (int k = 6; k >= 0; k--) y = y * 256 + b[i + k];
            return (b[i + 7] & 0x80) != 0 ? -y : y;
        }
    }

    // A plain bzip2 decoder (one stream, any number of blocks). CRCs aren't checked: the caller checks SHA-256.
    class Bzip2
    {
        readonly byte[] src;
        int pos, end, bitCount;
        uint bitBuf;

        Bzip2(byte[] data, int offset, int length) { src = data; pos = offset; end = offset + length; }

        public static byte[] Decompress(byte[] data, int offset, int length)
        {
            return new Bzip2(data, offset, length).Run();
        }

        int Bits(int n)
        {
            while (bitCount < n)
            {
                if (pos >= end) throw new InvalidDataException("bzip2: truncated");
                bitBuf = (bitBuf << 8) | src[pos++];
                bitCount += 8;
            }
            int v = (int)((bitBuf >> (bitCount - n)) & ((1u << n) - 1));
            bitCount -= n;
            return v;
        }

        static InvalidDataException Bad(string what) { return new InvalidDataException("bzip2: " + what); }

        byte[] Run()
        {
            var output = new MemoryStream();
            if (Bits(8) != 'B' || Bits(8) != 'Z' || Bits(8) != 'h') throw Bad("not bzip2 data");
            int level = Bits(8) - '0';
            if (level < 1 || level > 9) throw Bad("bad block size");
            int maxBlock = level * 100000;
            int[] tt = new int[maxBlock];

            while (true)
            {
                long magic = Bits(24) * 0x1000000L + Bits(24);   // 48 bits (no | on sign-extended values: a warning)
                Bits(16); Bits(16);   // block / stream CRC
                if (magic == 0x177245385090L) break;
                if (magic != 0x314159265359L) throw Bad("bad block header");
                if (Bits(1) != 0) throw Bad("randomised blocks aren't supported");
                int origPtr = Bits(24);

                // Which byte values occur in this block.
                byte[] seqToUnseq = new byte[256];
                int nInUse = 0, used = Bits(16);
                for (int i = 0; i < 16; i++)
                    if ((used & (0x8000 >> i)) != 0)
                    {
                        int bits = Bits(16);
                        for (int j = 0; j < 16; j++) if ((bits & (0x8000 >> j)) != 0) seqToUnseq[nInUse++] = (byte)(i * 16 + j);
                    }
                if (nInUse == 0) throw Bad("empty symbol map");
                int alphaSize = nInUse + 2;

                int nGroups = Bits(3), nSelectors = Bits(15);
                if (nGroups < 2 || nGroups > 6 || nSelectors < 1) throw Bad("bad Huffman groups");
                byte[] groupMtf = { 0, 1, 2, 3, 4, 5 };
                byte[] selectors = new byte[nSelectors];
                for (int i = 0; i < nSelectors; i++)
                {
                    int j = 0;
                    while (Bits(1) == 1) { j++; if (j >= nGroups) throw Bad("bad selector"); }
                    byte v = groupMtf[j];
                    for (; j > 0; j--) groupMtf[j] = groupMtf[j - 1];
                    groupMtf[0] = v;
                    selectors[i] = v;
                }

                // Canonical Huffman tables, one per group.
                int[][] limit = new int[nGroups][], baseCode = new int[nGroups][], perm = new int[nGroups][];
                int[] minLen = new int[nGroups], maxLen = new int[nGroups];
                for (int t = 0; t < nGroups; t++)
                {
                    int[] lens = new int[alphaSize];
                    int len = Bits(5);
                    for (int i = 0; i < alphaSize; i++)
                    {
                        while (true)
                        {
                            if (len < 1 || len > 20) throw Bad("bad code length");
                            if (Bits(1) == 0) break;
                            len += Bits(1) == 0 ? 1 : -1;
                        }
                        lens[i] = len;
                    }
                    int lo = 32, hi = 0;
                    foreach (int l in lens) { if (l < lo) lo = l; if (l > hi) hi = l; }
                    minLen[t] = lo; maxLen[t] = hi;
                    limit[t] = new int[hi + 2]; baseCode[t] = new int[hi + 2]; perm[t] = new int[alphaSize];
                    int code = 0, k = 0;
                    for (int l = lo; l <= hi; l++)
                    {
                        baseCode[t][l] = code - k;
                        for (int s = 0; s < alphaSize; s++) if (lens[s] == l) { perm[t][k++] = s; code++; }
                        limit[t][l] = code - 1;
                        code <<= 1;
                    }
                }

                // Huffman symbols -> run lengths (RUNA/RUNB) and move-to-front indexes -> bytes.
                byte[] mtf = new byte[256];
                for (int i = 0; i < 256; i++) mtf[i] = (byte)i;
                int[] counts = new int[256];
                int eob = nInUse + 1, count = 0, groupIndex = -1, groupLeft = 0, group = 0, run = 0, runBit = 1;
                while (true)
                {
                    if (groupLeft == 0)
                    {
                        if (++groupIndex >= nSelectors) throw Bad("ran out of selectors");
                        group = selectors[groupIndex];
                        groupLeft = 50;
                    }
                    groupLeft--;
                    int n = minLen[group], v = Bits(n), sym;
                    while (true)
                    {
                        if (n > maxLen[group]) throw Bad("bad Huffman code");
                        if (v <= limit[group][n]) { sym = perm[group][v - baseCode[group][n]]; break; }
                        v = (v << 1) | Bits(1);
                        n++;
                    }

                    if (sym <= 1)
                    {
                        run += (sym + 1) * runBit;
                        runBit <<= 1;
                        if (run > maxBlock) throw Bad("run too long");
                        continue;
                    }
                    if (run > 0)
                    {
                        byte b = seqToUnseq[mtf[0]];
                        if (count + run > maxBlock) throw Bad("block too long");
                        counts[b] += run;
                        for (; run > 0; run--) tt[count++] = b;
                        runBit = 1;
                    }
                    if (sym == eob) break;
                    int idx = sym - 1;
                    byte m = mtf[idx];
                    for (int i = idx; i > 0; i--) mtf[i] = mtf[i - 1];
                    mtf[0] = m;
                    byte ch = seqToUnseq[m];
                    if (count >= maxBlock) throw Bad("block too long");
                    counts[ch]++;
                    tt[count++] = ch;
                }
                if (origPtr >= count) throw Bad("bad origin pointer");

                // Inverse Burrows-Wheeler, then undo the initial run-length encoding (4 equal bytes + a count).
                int[] next = new int[256];
                for (int i = 0, sum = 0; i < 256; i++) { next[i] = sum; sum += counts[i]; }
                for (int i = 0; i < count; i++) { int b = tt[i] & 0xFF; tt[next[b]++] |= i << 8; }
                int tPos = tt[origPtr] >> 8, last = -1, same = 0;
                for (int i = 0; i < count; i++)
                {
                    tPos = tt[tPos];
                    int b = tPos & 0xFF;
                    tPos >>= 8;
                    if (same == 4)
                    {
                        for (int r = 0; r < b; r++) output.WriteByte((byte)last);
                        same = 0; last = -1;
                    }
                    else if (b == last) { same++; output.WriteByte((byte)b); }
                    else { last = b; same = 1; output.WriteByte((byte)b); }
                }
            }
            return output.ToArray();
        }
    }
}
