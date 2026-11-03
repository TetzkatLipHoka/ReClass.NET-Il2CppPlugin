using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ReClassNET.Memory;
using ReClassNET.Nodes;
using ReClassNET.Plugins;

namespace Il2CppPlugin
{
	/// <summary>
	/// Annotates pointers to Unity IL2CPP objects with their class (Namespace.Outer.Name) and shows System.String values.
	/// Needs no dump data: an Il2CppObject starts with its Il2CppClass*, and the class carries name/namespace pointers at
	/// fixed offsets (measured on metadata v29/v31/v39, x64: name 0x10, namespace 0x18, declaringType 0x50, parent 0x58).
	/// </summary>
	public class Il2CppPluginExt : Plugin
	{
		public override IReadOnlyList<INodeInfoReader> GetNodeInfoReaders() => new[] { new Il2CppNodeInfoReader() };
	}

	public class Il2CppNodeInfoReader : INodeInfoReader
	{
		private const int KlassName = 0x10, KlassNamespace = 0x18, KlassDeclaring = 0x50, KlassParent = 0x58;
		private const int StringLength = 0x10, StringChars = 0x14;

		// klass -> full name; null marks a pointer that was checked and is not a class
		private readonly Dictionary<IntPtr, string> cache = new Dictionary<IntPtr, string>();

		public string ReadNodeInfo(BaseHexCommentNode node, IRemoteMemoryReader reader, MemoryBuffer memory, IntPtr nodeAddress, IntPtr nodeValue)
		{
			if (!Plausible(nodeValue)) return null;
			var klass = Ptr(reader, nodeValue);
			if (!Plausible(klass)) return null;

			if (!cache.TryGetValue(klass, out var name))
			{
				if (cache.Count > 50000) cache.Clear();
				name = LooksLikeClass(reader, klass, 0) ? FullName(reader, klass, 0) : null;
				cache[klass] = name;
			}
			if (name == null) return null;

			if (name == "System.String")
			{
				var len = I32(reader, nodeValue + StringLength);
				if (len >= 0 && len < 4096)
				{
					var raw = reader.ReadRemoteMemory(nodeValue + StringChars, len * 2);
					if (raw != null && raw.Length == len * 2)
					{
						return $"String \"{Encoding.Unicode.GetString(raw)}\"";
					}
				}
			}
			return name;
		}

		private static bool Plausible(IntPtr p) => (long)p > 0x10000 && (long)p < 0x7FFF_FFFF_FFFF && ((long)p & 7) == 0;

		private static IntPtr Ptr(IRemoteMemoryReader r, IntPtr a)
		{
			var b = r.ReadRemoteMemory(a, 8);
			return b != null && b.Length == 8 ? (IntPtr)BitConverter.ToInt64(b, 0) : IntPtr.Zero;
		}

		private static int I32(IRemoteMemoryReader r, IntPtr a)
		{
			var b = r.ReadRemoteMemory(a, 4);
			return b != null && b.Length == 4 ? BitConverter.ToInt32(b, 0) : -1;
		}

		/// <summary>C string if it is a plausible type identifier (letters, digits, and the characters IL2CPP mangles with).</summary>
		private static string Identifier(IRemoteMemoryReader r, IntPtr a, bool allowEmpty)
		{
			if (a == IntPtr.Zero) return null;
			var raw = r.ReadRemoteMemory(a, 128);
			if (raw == null) return null;
			var len = Array.IndexOf(raw, (byte)0);
			if (len < 0) return null;                 // longer than any sane identifier
			if (len == 0) return allowEmpty ? "" : null;
			for (var i = 0; i < len; i++)
			{
				var c = (char)raw[i];
				if (!(char.IsLetterOrDigit(c) || c == '_' || c == '`' || c == '<' || c == '>' || c == '.' || c == '$' || c == '-' || c == '=' || c == '|' || c == ','))
				{
					return null;
				}
			}
			return Encoding.ASCII.GetString(raw, 0, len);
		}

		private static bool LooksLikeClass(IRemoteMemoryReader r, IntPtr klass, int depth)
		{
			if (!Plausible(klass) || depth > 3) return false;
			if (!Plausible(Ptr(r, klass))) return false;                       // Il2CppClass.image
			if (Identifier(r, Ptr(r, klass + KlassName), false) == null) return false;
			if (Identifier(r, Ptr(r, klass + KlassNamespace), true) == null) return false;
			var parent = Ptr(r, klass + KlassParent);
			return parent == IntPtr.Zero || LooksLikeClass(r, parent, depth + 1);
		}

		private static string FullName(IRemoteMemoryReader r, IntPtr klass, int depth)
		{
			var name = Identifier(r, Ptr(r, klass + KlassName), false) ?? "?";
			var declaring = Ptr(r, klass + KlassDeclaring);
			if (Plausible(declaring) && depth < 8 && LooksLikeClass(r, declaring, 0))
			{
				return $"{FullName(r, declaring, depth + 1)}.{name}";
			}
			var ns = Identifier(r, Ptr(r, klass + KlassNamespace), true);
			return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
		}
	}
}
