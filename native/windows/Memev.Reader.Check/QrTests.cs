// SPDX-License-Identifier: Apache-2.0
// The window's QR symbol against the core's: each SHA-256 is of the rows of 0 and 1 that the core's vendor/qr.mjs qrMatrix draws
// for the same text, at memev ceb322f853488ee83162a5d6b39169243bb7ace7.
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Memev.Reader;

static class QrTests
{
  public static void Run(Action<bool, string> expect)
  {
    var cases = new (string Text, int Size, string Sha)[]
    {
      ("https://t.me/memev_bot?start=abc123", 29, "dfe153542a39797d7f3607ec1e9ff72bc9d3dd6976f68e990c1305667dc07f17"),
      ("2@" + new string('A', 180) + ",xyz", 49, "494ba33e70b42a824b61ee171738397ccfb1209143c72c35538c4bba548fd224"),
      ("x", 21, "5dfe8ef791abc17390b85f13f5d69ef3087e5a2bfd00f1d669a4fbcae8943d72"),
      (new string('B', 300), 61, "ec160335274ef7f936118f26db46ca11d7c781dc0f36b63b10ea920f1bf19d6e"),
    };
    foreach (var (text, size, sha) in cases)
    {
      string rows = Qr.Rows(text);
      expect(Qr.Matrix(text).GetLength(0) == size, $"qr size for {text.Length} bytes");
      string got = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(rows))).ToLowerInvariant();
      expect(got == sha, $"qr matrix for {text.Length} bytes matches the core's");
    }
    bool threw = false;
    try { Qr.Matrix(new string('C', 500)); } catch (ArgumentException) { threw = true; }
    expect(threw, "a text too long for a QR code is refused");
  }
}
