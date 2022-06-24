﻿using Localizer.DataExtractors;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Localizer.Localizers
{
    public class JarGeneralLocalizer
    {
        public int Localize(string zipPath, IDictionary<string, string> dictionary)
        {
            int localizedStrings = 0;
            Console.WriteLine($"Process: {zipPath}");

            using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Update))
            {
                foreach (ZipArchiveEntry entry in archive.Entries.ToList())
                {
                    if (entry.FullName.EndsWith(".class", StringComparison.OrdinalIgnoreCase))
                    {
                        var stream = entry.Open();
                        byte[] sourceData = ReadFully(stream);
                        var javaClassExtractor = new JavaClassExtractor(sourceData);
                        var utf8Strings = javaClassExtractor.GetUtf8Entries();

                        int modified = 0;
                        foreach(var text in utf8Strings)
                            if (dictionary.TryGetValue(text, out string translate))
                            {
                                Console.WriteLine($"[LOCALIZED] \"{text}\" - \"{translate}\"");
                                var textBytes = Encoding.UTF8.GetBytes(text);
                                var translateBytes = Encoding.UTF8.GetBytes(translate);

                                textBytes = BitConverter.GetBytes(((ushort)textBytes.Length)).Reverse().Concat(textBytes).ToArray();
                                translateBytes = BitConverter.GetBytes(((ushort)translateBytes.Length)).Reverse().Concat(translateBytes).ToArray();

                                /*int pos = FindBytes(sourceData, textBytes);

                                var span = new ReadOnlySpan<byte>(sourceData, pos,2);
                                byte[] reverse = new byte[] { sourceData[pos + 1], sourceData[pos] };
                                ushort len = BitConverter.ToUInt16(reverse);
                                Console.WriteLine($"[LEN MATCH]: {len == textBytes.Length - 2}\"");*/

                                sourceData = ReplaceBytes(sourceData, textBytes, translateBytes);
                                modified++;
                            }

                        if(modified > 0)
                        {
                            stream.Position = 0;
                            stream.SetLength(0);
                            stream.Write(sourceData, 0, sourceData.Length);
                            stream.Flush();
                            stream.Close();
                        }
                    }
                }
            }

            return localizedStrings;
        }

        private static byte[] ReadFully(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        public static int FindBytes(byte[] src, byte[] find, int startIndex = 0)
        {
            int index = -1;
            int matchIndex = 0;
            // handle the complete source array
            for (int i = startIndex; i < src.Length; i++)
            {
                if (src[i] == find[matchIndex])
                {
                    if (matchIndex == (find.Length - 1))
                    {
                        index = i - matchIndex;
                        break;
                    }
                    matchIndex++;
                }
                else if (src[i] == find[0])
                {
                    matchIndex = 1;
                }
                else
                {
                    matchIndex = 0;
                }

            }
            return index;
        }

        public static byte[] ReplaceBytes(byte[] src, byte[] search, byte[] repl)
        {
            byte[] dst = null;
            int index = FindBytes(src, search);
            if (index >= 0)
            {
                dst = new byte[src.Length - search.Length + repl.Length];
                // before found array
                Buffer.BlockCopy(src, 0, dst, 0, index);
                // repl copy
                Buffer.BlockCopy(repl, 0, dst, index, repl.Length);
                // rest of src array
                Buffer.BlockCopy(
                    src,
                    index + search.Length,
                    dst,
                    index + repl.Length,
                    src.Length - (index + search.Length));
            }
            return dst;
        }
    }
}
