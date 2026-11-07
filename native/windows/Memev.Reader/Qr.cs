// SPDX-License-Identifier: Apache-2.0
// A byte-mode QR symbol, error correction L, versions 1 to 9, 11 and 13, mask 0: the same symbol the core's own vendor/qr.mjs
// draws for a Telegram pairing link or a WhatsApp link code, so the window can show it as a picture. The matrix is checked
// against the core's in Memev.Reader.Check.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Memev.Reader;

public static class Qr
{
  sealed record Spec(int Data, int Ecc, int Blocks, int[] Align);

  static readonly SortedDictionary<int, Spec> Specs = new()
  {
    [1] = new(19, 7, 1, Array.Empty<int>()),
    [2] = new(34, 10, 1, new[] { 6, 18 }),
    [3] = new(55, 15, 1, new[] { 6, 22 }),
    [4] = new(80, 20, 1, new[] { 6, 26 }),
    [5] = new(108, 26, 1, new[] { 6, 30 }),
    [6] = new(136, 18, 2, new[] { 6, 34 }),
    [7] = new(156, 20, 2, new[] { 6, 22, 38 }),
    [8] = new(194, 24, 2, new[] { 6, 24, 42 }),
    [9] = new(232, 30, 2, new[] { 6, 26, 46 }),
    [11] = new(324, 20, 4, new[] { 6, 30, 54 }),
    [13] = new(428, 26, 4, new[] { 6, 34, 62 }),
  };

  // byte mode counts its length in 8 bits up to version 9 and in 16 bits from version 10
  static int CountBits(int version) => version >= 10 ? 16 : 8;

  static readonly byte[] Exp = new byte[512];
  static readonly byte[] Log = new byte[256];

  static Qr()
  {
    int x = 1;
    for (int i = 0; i < 255; i++) { Exp[i] = (byte)x; Log[x] = (byte)i; x <<= 1; if ((x & 0x100) != 0) x ^= 0x11d; }
    for (int i = 255; i < 512; i++) Exp[i] = Exp[i - 255];
  }

  static int Mul(int a, int b) => a != 0 && b != 0 ? Exp[Log[a] + Log[b]] : 0;

  static int[] Generator(int nsym)
  {
    var poly = new List<int> { 1 };
    for (int i = 0; i < nsym; i++)
    {
      var next = new int[poly.Count + 1];
      for (int j = 0; j < poly.Count; j++) { next[j] ^= poly[j]; next[j + 1] ^= Mul(poly[j], Exp[i]); }
      poly = next.ToList();
    }
    return poly.ToArray();
  }

  static int[] Remainder(IEnumerable<int> data, int nsym)
  {
    int[] gen = Generator(nsym);
    var res = new int[nsym];
    foreach (int b in data)
    {
      int factor = b ^ res[0];
      for (int i = 0; i < nsym - 1; i++) res[i] = res[i + 1] ^ (factor != 0 ? Mul(gen[i + 1], factor) : 0);
      res[nsym - 1] = factor != 0 ? Mul(gen[nsym], factor) : 0;
    }
    return res;
  }

  static List<int> DataCodewords(byte[] payload, int count, int version)
  {
    var bits = new List<int>();
    void Push(int value, int n) { for (int i = n - 1; i >= 0; i--) bits.Add((value >> i) & 1); }
    Push(0b0100, 4);
    Push(payload.Length, CountBits(version));
    foreach (byte b in payload) Push(b, 8);
    int cap = count * 8;
    if (bits.Count > cap) throw new ArgumentException("the text is too long for a QR code");
    int term = Math.Min(4, cap - bits.Count);
    for (int i = 0; i < term; i++) bits.Add(0);
    while (bits.Count % 8 != 0) bits.Add(0);
    var output = new List<int>();
    for (int i = 0; i < bits.Count; i += 8) output.Add(bits.Skip(i).Take(8).Aggregate(0, (n, bit) => (n << 1) | bit));
    for (int pad = 0; output.Count < count; pad++) output.Add(pad % 2 != 0 ? 0x11 : 0xec);
    return output;
  }

  static List<int> Codewords(byte[] payload, Spec spec, int version)
  {
    var data = DataCodewords(payload, spec.Data, version);
    int size = spec.Data / spec.Blocks;
    var blocks = new List<List<int>>();
    var eccs = new List<int[]>();
    for (int i = 0; i < spec.Blocks; i++)
    {
      var block = data.Skip(i * size).Take(size).ToList();
      blocks.Add(block);
      eccs.Add(Remainder(block, spec.Ecc));
    }
    var output = new List<int>();
    for (int i = 0; i < size; i++) foreach (var b in blocks) output.Add(b[i]);
    for (int i = 0; i < spec.Ecc; i++) foreach (var e in eccs) output.Add(e[i]);
    return output;
  }

  static int FormatBits(int mask)
  {
    int data = (1 << 3) | mask; // error correction L is 01
    int rem = data << 10;
    for (int i = 14; i >= 10; i--) if (((rem >> i) & 1) != 0) rem ^= 0b10100110111 << (i - 10);
    return (((data << 10) | (rem & 0x3ff)) ^ 0b101010000010010) & 0x7fff;
  }

  static int VersionBits(int version)
  {
    int rem = version << 12;
    for (int i = 17; i >= 12; i--) if (((rem >> i) & 1) != 0) rem ^= 0b1111100100101 << (i - 12);
    return ((version << 12) | (rem & 0xfff)) & 0x3ffff;
  }

  static void Finder(bool[,] mod, bool[,] res, int r0, int c0)
  {
    int n = mod.GetLength(0);
    for (int r = -1; r <= 7; r++)
      for (int c = -1; c <= 7; c++)
      {
        int rr = r0 + r, cc = c0 + c;
        if (rr < 0 || cc < 0 || rr >= n || cc >= n) continue;
        bool on = r >= 0 && r <= 6 && c >= 0 && c <= 6 && (r == 0 || r == 6 || c == 0 || c == 6 || (r >= 2 && r <= 4 && c >= 2 && c <= 4));
        mod[rr, cc] = on;
        res[rr, cc] = true;
      }
  }

  static void Align(bool[,] mod, bool[,] res, int cx, int cy)
  {
    if (res[cy, cx]) return;
    for (int r = -2; r <= 2; r++)
      for (int c = -2; c <= 2; c++)
      {
        mod[cy + r, cx + c] = Math.Max(Math.Abs(r), Math.Abs(c)) != 1;
        res[cy + r, cx + c] = true;
      }
  }

  static void ReserveFormat(bool[,] res)
  {
    int n = res.GetLength(0);
    for (int i = 0; i <= 8; i++) if (i != 6) { res[8, i] = true; res[i, 8] = true; }
    for (int i = 0; i < 8; i++) { res[8, n - 1 - i] = true; res[n - 1 - i, 8] = true; }
    res[n - 8, 8] = true;
  }

  static void DrawFormat(bool[,] mod, int bits)
  {
    int n = mod.GetLength(0);
    bool Bit(int i) => ((bits >> (14 - i)) & 1) == 1;
    int[][] seq = { new[] { 8, 0 }, new[] { 8, 1 }, new[] { 8, 2 }, new[] { 8, 3 }, new[] { 8, 4 }, new[] { 8, 5 }, new[] { 8, 7 }, new[] { 8, 8 }, new[] { 7, 8 }, new[] { 5, 8 }, new[] { 4, 8 }, new[] { 3, 8 }, new[] { 2, 8 }, new[] { 1, 8 }, new[] { 0, 8 } };
    var seq2 = new List<int[]>();
    for (int i = 0; i < 7; i++) seq2.Add(new[] { n - 1 - i, 8 });
    for (int i = 0; i < 8; i++) seq2.Add(new[] { 8, n - 8 + i });
    for (int i = 0; i < 15; i++) { mod[seq[i][0], seq[i][1]] = Bit(i); mod[seq2[i][0], seq2[i][1]] = Bit(i); }
    mod[n - 8, 8] = true;
  }

  static void DrawVersion(bool[,] mod, bool[,] res, int version)
  {
    if (version < 7) return;
    int bits = VersionBits(version);
    int n = mod.GetLength(0);
    for (int i = 0; i < 18; i++)
    {
      bool on = ((bits >> i) & 1) == 1;
      int a = i / 3, b = i % 3;
      mod[a, n - 11 + b] = on; res[a, n - 11 + b] = true;
      mod[n - 11 + b, a] = on; res[n - 11 + b, a] = true;
    }
  }

  static void PlaceData(bool[,] mod, bool[,] res, List<int> words, int version)
  {
    var bits = new List<int>();
    foreach (int w in words) for (int i = 7; i >= 0; i--) bits.Add((w >> i) & 1);
    int extra = version == 1 ? 0 : 7;
    for (int i = 0; i < extra; i++) bits.Add(0);
    int n = mod.GetLength(0);
    int k = 0;
    bool up = true;
    for (int col = n - 1; col > 0; col -= 2)
    {
      if (col == 6) col--;
      for (int step = 0; step < n; step++)
      {
        int row = up ? n - 1 - step : step;
        foreach (int c in new[] { col, col - 1 })
        {
          if (res[row, c]) continue;
          int bit = k < bits.Count ? bits[k++] : 0;
          if (((row + c) & 1) == 0) bit ^= 1; // mask 0
          mod[row, c] = bit == 1;
        }
      }
      up = !up;
    }
  }

  /// A matrix, true for a dark module, with no quiet zone. Throws ArgumentException when the text does not fit.
  public static bool[,] Matrix(string text)
  {
    byte[] payload = Encoding.UTF8.GetBytes(text);
    int version = 0;
    Spec? spec = null;
    foreach (var (v, s) in Specs)
      if (s.Data * 8 >= 4 + CountBits(v) + payload.Length * 8) { version = v; spec = s; break; }
    if (spec == null) throw new ArgumentException("the text is too long for a QR code");
    int n = 17 + 4 * version;
    var mod = new bool[n, n];
    var res = new bool[n, n];
    Finder(mod, res, 0, 0);
    Finder(mod, res, 0, n - 7);
    Finder(mod, res, n - 7, 0);
    foreach (int y in spec.Align) foreach (int x in spec.Align) if (!res[y, x]) Align(mod, res, x, y);
    for (int i = 0; i < n; i++)
    {
      if (!res[6, i]) { mod[6, i] = i % 2 == 0; res[6, i] = true; }
      if (!res[i, 6]) { mod[i, 6] = i % 2 == 0; res[i, 6] = true; }
    }
    ReserveFormat(res);
    DrawVersion(mod, res, version);
    PlaceData(mod, res, Codewords(payload, spec, version), version);
    DrawFormat(mod, FormatBits(0));
    return mod;
  }

  /// One line of 0 and 1 per row, for comparing with the core's matrix.
  public static string Rows(string text)
  {
    var m = Matrix(text);
    int n = m.GetLength(0);
    var sb = new StringBuilder();
    for (int y = 0; y < n; y++)
    {
      for (int x = 0; x < n; x++) sb.Append(m[y, x] ? '1' : '0');
      sb.Append('\n');
    }
    return sb.ToString();
  }
}
