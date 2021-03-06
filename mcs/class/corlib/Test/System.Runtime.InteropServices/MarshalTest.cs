//
// System.Runtime.InteropServices.Marshal Test Cases
//
// Authors:
// 	Gonzalo Paniagua Javier (gonzalo@ximian.com)
//	Sebastien Pouliot  <sebastien@ximian.com>
//
// Copyright (C) 2004-2007 Novell, Inc (http://www.novell.com)
//
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
#if !MOBILE
using System.Reflection.Emit;
#endif
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace MonoTests.System.Runtime.InteropServices
{
	[TestFixture]
	public class MarshalTest
	{
		[StructLayout (LayoutKind.Sequential)]
		class ClsSequential {
			public int field;
		}

		public class ClsNoLayout {
			public int field;
		}

		[StructLayout (LayoutKind.Explicit)]
		class ClsExplicit {
			[FieldOffset (0)] public int field;
		}

		[StructLayout (LayoutKind.Sequential)]
		struct StrSequential {
			public int field;
		}

		struct StrNoLayout {
			public int field;
		}

		[StructLayout (LayoutKind.Explicit)]
		struct StrExplicit {
			[FieldOffset (0)] public int field;
		}

		[Test]
		public void SizeOf_Class_LayoutSequential ()
		{
			Marshal.SizeOf (typeof (ClsSequential));
		}

		[Test]
		public void SizeOf_Class_LayoutNotSet ()
		{
			try {
				Marshal.SizeOf (typeof (ClsNoLayout));
				Assert.Fail ("#1");
			} catch (ArgumentException ex) {
				// Type '...MarshalTest+ClsNoLayout' cannot be
				// marshaled as an unmanaged structure; no
				// meaningful size or offset can be computed
				Assert.AreEqual (typeof (ArgumentException), ex.GetType (), "#2");
				Assert.IsNull (ex.InnerException, "#3");
				Assert.IsNotNull (ex.Message, "#4");
			}
		}

		[Test]
		public void SizeOf_Class_LayoutExplicit ()
		{
			Marshal.SizeOf (typeof (ClsExplicit));
		}

		[Test]
		public void SizeOf_Struct_LayoutSequential ()
		{
			Marshal.SizeOf (typeof (StrSequential));
		}

		[Test]
		public void SizeOf_Struct_LayoutNotSet ()
		{
			Marshal.SizeOf (typeof (StrNoLayout));
		}

		[Test]
		public void SizeOf_Struct_LayoutExplicit ()
		{
			Marshal.SizeOf (typeof (StrExplicit));
		}

		[Test]
		public void SizeOf_Array ()
		{
			try {
				Marshal.SizeOf (typeof (string []));
				Assert.Fail ("#1");
			} catch (ArgumentException ex) {
				// Type 'System.String[]' cannot be marshaled
				// as an unmanaged structure; no meaningful
				// size or offset can be computed
				Assert.AreEqual (typeof (ArgumentException), ex.GetType (), "#2");
				Assert.IsNull (ex.InnerException, "#3");
				Assert.IsNotNull (ex.Message, "#4");
			}
		}

		[Test]
		public unsafe void Sizeof_Pointer ()
		{
			int size = Marshal.SizeOf (typeof (char*));
			Assert.IsTrue (size == 4 || size == 8);
		}

		[Test]
		public void PtrToStringWithNull ()
		{
			Assert.IsNull (Marshal.PtrToStringAnsi (IntPtr.Zero), "A");
			Assert.IsNull (Marshal.PtrToStringUni (IntPtr.Zero), "C");
		}

		[Test]
		public void PtrToStringAnsi_Ptr_Zero ()
		{
			try {
				Marshal.PtrToStringAnsi (IntPtr.Zero, 0);
				Assert.Fail ("#1");
			} catch (ArgumentNullException ex) {
				Assert.AreEqual (typeof (ArgumentNullException), ex.GetType (), "#2");
				Assert.IsNull (ex.InnerException, "#3");
				Assert.IsNotNull (ex.Message, "#4");
				Assert.AreEqual ("ptr", ex.ParamName, "#5");
			}
		}

		[Test]
		public void PtrToStringWithUni_Ptr_Zero ()
		{
			try {
				Marshal.PtrToStringUni (IntPtr.Zero, 0);
				Assert.Fail ("#1");
			} catch (ArgumentNullException ex) {
				Assert.AreEqual (typeof (ArgumentNullException), ex.GetType (), "#2");
				Assert.IsNull (ex.InnerException, "#3");
				Assert.IsNotNull (ex.Message, "#4");
				Assert.AreEqual ("ptr", ex.ParamName, "#5");
			}
		}

		readonly String[] TestStrings = new String[] {
			"", //Empty String
			"Test String",
			"A", //Single character string
			"This is a very long string as it repeats itself. " +
			"This is a very long string as it repeats itself. " +
			"This is a very long string as it repeats itself. " +
			"This is a very long string as it repeats itself. " +
			"This is a very long string as it repeats itself. " +
			"This is a very long string as it repeats itself. " +
			"This is a very long string as it repeats itself. " +
			"This is a very long string as it repeats itself. " +
			"This is a very long string as it repeats itself. " +
			"This is a very long string as it repeats itself. " +
			"This is a very long string as it repeats itself. " +
			"This is a very long string as it repeats itself. " +
			"This is a very long string as it repeats itself.",
			"This \n is \n a \n multiline \n string",
			"This \0 is \0 a \0 string \0 with \0 nulls",
			"\0string",
			"string\0",
			"\0\0\0\0\0\0\0\0"
		};

		[Test]
		public unsafe void PtrToStringUTF8_Test ()
		{
			int i = 0; 
			foreach (String srcString in TestStrings)
			{
				i++;
				// we assume string null terminated
				if (srcString.Contains("\0"))
					continue;

				IntPtr ptrString = Marshal.StringToAllocatedMemoryUTF8(srcString);
				string retString = Marshal.PtrToStringUTF8(ptrString);

				Assert.AreEqual (srcString, retString, "#" + i);
				if (srcString.Length > 0)
				{
					string retString2 = Marshal.PtrToStringUTF8(ptrString, srcString.Length - 1);
					Assert.AreEqual (srcString.Substring(0, srcString.Length - 1), retString2, "#s" + i);
				}
				Marshal.FreeHGlobal(ptrString);
			}			
		}
		
		[Test]
		public unsafe void UnsafeAddrOfPinnedArrayElement ()
		{
			short[] sarr = new short [5];
			sarr [2] = 3;

			IntPtr ptr = Marshal.UnsafeAddrOfPinnedArrayElement (sarr, 2);
			Assert.AreEqual (3, *(short*) ptr.ToPointer ());
		}

		[Test]
		public void AllocHGlobalZeroSize ()
		{
			IntPtr ptr = Marshal.AllocHGlobal (0);
			Assert.IsTrue (ptr != IntPtr.Zero);
			Marshal.FreeHGlobal (ptr);
		}

		[Test]
		public void AllocCoTaskMemZeroSize ()
		{
			IntPtr ptr = Marshal.AllocCoTaskMem (0);
			Assert.IsTrue (ptr != IntPtr.Zero);
			Marshal.FreeCoTaskMem (ptr);
		}

		public struct Foo {
			public int a;
			public static int b;
			public long c;
			public static char d;
			public int e;
		}

		[Test]
		public void OffsetOf_FieldName_Static ()
		{
			try {
				Marshal.OffsetOf (typeof (Foo), "b");
				Assert.Fail ("#1");
			} catch (ArgumentException ex) {
				// Field passed in is not a marshaled member of
				// the type '...MarshalTest+Foo'
				Assert.AreEqual (typeof (ArgumentException), ex.GetType (), "#2");
				Assert.IsNull (ex.InnerException, "#3");
				Assert.IsNotNull (ex.Message, "#4");
				Assert.AreEqual ("fieldName", ex.ParamName, "#5");
			}
		}
#if !MOBILE
		[Test]
		public void GetHINSTANCE ()
		{
			if (RunningOnMono)
				Assert.Ignore ("GetHINSTANCE only applies to .NET on Windows.");

			Assembly a;
			IntPtr hinstance;
			StringBuilder fileName;

			fileName = new StringBuilder (255);
			a = Assembly.GetExecutingAssembly ();
			hinstance = Marshal.GetHINSTANCE (a.GetModules () [0]);
			Assert.IsTrue (GetModuleFileName (hinstance, fileName,
				fileName.Capacity) > 0, "#A1");
			Assert.AreEqual (a.Location, fileName.ToString (), "#A2");

			fileName.Length = 0;
			a = typeof (int).Assembly;
			hinstance = Marshal.GetHINSTANCE (a.GetModules () [0]);
			Assert.IsTrue (GetModuleFileName (hinstance, fileName,
				fileName.Capacity) > 0, "#B1");
			Assert.IsTrue (File.Exists (fileName.ToString ()), "#B3");
			Assert.AreEqual ("mscorlib.dll", Path.GetFileName (fileName.ToString ()), "#B4");
		}

		[Test]
		public void GetHINSTANCE_Module_Dynamic ()
		{
			AssemblyName aname = new AssemblyName ();
			aname.Name = "foo";

			AssemblyBuilder ab = AppDomain.CurrentDomain.DefineDynamicAssembly (
				aname, AssemblyBuilderAccess.Save,
				Path.GetTempPath ());
			ModuleBuilder mb = ab.DefineDynamicModule ("foo.dll", false);

			IntPtr hinstance = Marshal.GetHINSTANCE (mb);
			Assert.AreEqual (-1, hinstance.ToInt32 ());
		}

		[Test]
		public void GetHINSTANCE_Module_Null ()
		{
			try {
				Marshal.GetHINSTANCE ((Module) null);
				Assert.Fail ("#1");
			} catch (ArgumentNullException ex) {
				Assert.AreEqual (typeof (ArgumentNullException), ex.GetType (), "#2");
				Assert.IsNull (ex.InnerException, "#3");
				Assert.IsNotNull (ex.Message, "#4");
				Assert.AreEqual ("m", ex.ParamName, "#5");
			}
		}
#endif

		[Test]
		public void GetHRForException ()
		{
			Assert.AreEqual (0, Marshal.GetHRForException (null));
			Assert.IsTrue (Marshal.GetHRForException (new Exception ()) < 0);
			Assert.AreEqual (12345, Marshal.GetHRForException (new IOException ("test message", 12345)));
		}

		[Test] // bug #319009
		public void StringToHGlobalUni ()
		{
			IntPtr handle = Marshal.StringToHGlobalUni ("unicode data");
			string s = Marshal.PtrToStringUni (handle);
			Assert.AreEqual (12, s.Length, "#1");

			handle = Marshal.StringToHGlobalUni ("unicode data string");
			s = Marshal.PtrToStringUni (handle);
			Assert.AreEqual (19, s.Length, "#2");
		}

		[Test]
		public void ReadIntByte ()
		{
			IntPtr ptr = Marshal.AllocHGlobal (4);
			try {
				Marshal.WriteByte (ptr, 0, 0x1);
				Marshal.WriteByte (ptr, 1, 0x2);
				Assert.AreEqual (0x1, Marshal.ReadByte (ptr));
				Assert.AreEqual (0x1, Marshal.ReadByte (ptr, 0));
				Assert.AreEqual (0x2, Marshal.ReadByte (ptr, 1));
			} finally {
				Marshal.FreeHGlobal (ptr);
			}
		}

		[Test]
		public void ReadInt16 ()
		{
			IntPtr ptr = Marshal.AllocHGlobal (64);
			try {
				Marshal.WriteInt16 (ptr, 0, 0x1234);
				Marshal.WriteInt16 (ptr, 2, 0x4567);
				Marshal.WriteInt16 (ptr, 5, 0x4567);
				Assert.AreEqual (0x1234, Marshal.ReadInt16 (ptr));
				Assert.AreEqual (0x1234, Marshal.ReadInt16 (ptr, 0));
				Assert.AreEqual (0x4567, Marshal.ReadInt16 (ptr, 2));
				Assert.AreEqual (0x4567, Marshal.ReadInt16 ((ptr + 5)));
				Assert.AreEqual (0x4567, Marshal.ReadInt16 (ptr, 5));
			} finally {
				Marshal.FreeHGlobal (ptr);
			}
		}

		[Test]
		public void ReadInt32 ()
		{
			IntPtr ptr = Marshal.AllocHGlobal (64);
			try {
				Marshal.WriteInt32 (ptr, 0, 0x12345678);
				Marshal.WriteInt32 (ptr, 4, 0x77654321);
				Marshal.WriteInt32 (ptr, 10, 0x77654321);
				Assert.AreEqual (0x12345678, Marshal.ReadInt32 (ptr));
				Assert.AreEqual (0x12345678, Marshal.ReadInt32 (ptr, 0));
				Assert.AreEqual (0x77654321, Marshal.ReadInt32 (ptr, 4));
				Assert.AreEqual (0x77654321, Marshal.ReadInt32 ((ptr + 10)));
				Assert.AreEqual (0x77654321, Marshal.ReadInt32 (ptr, 10));
			} finally {
				Marshal.FreeHGlobal (ptr);
			}
		}

		[Test]
		public void ReadInt32_Endian ()
		{
			IntPtr ptr = Marshal.AllocHGlobal (4);
			try {
				Marshal.WriteByte (ptr, 0, 0x01);
				Marshal.WriteByte (ptr, 1, 0x02);
				Marshal.WriteByte (ptr, 2, 0x03);
				Marshal.WriteByte (ptr, 3, 0x04);
				// Marshal MUST use the native CPU data
				if (BitConverter.IsLittleEndian){
					Assert.AreEqual (0x04030201, Marshal.ReadInt32 (ptr), "ReadInt32");
				} else {
					Assert.AreEqual (0x01020304, Marshal.ReadInt32 (ptr), "ReadInt32");
				}
			} finally {
				Marshal.FreeHGlobal (ptr);
			}
		}

		[Test]
		public void ReadInt64 ()
		{
			IntPtr ptr = Marshal.AllocHGlobal (16);
			try {
				Marshal.WriteInt64 (ptr, 0, 0x12345678ABCDEFL);
				Marshal.WriteInt64 (ptr, 8, 0x87654321ABCDEFL);
				Assert.AreEqual (0x12345678ABCDEFL, Marshal.ReadInt64 (ptr));
				Assert.AreEqual (0x12345678ABCDEFL, Marshal.ReadInt64 (ptr, 0));
				Assert.AreEqual (0x87654321ABCDEFL, Marshal.ReadInt64 (ptr, 8));
			} finally {
				Marshal.FreeHGlobal (ptr);
			}
		}

		[Test]
		[Category ("MobileNotWorking")]
		public void BSTR_Roundtrip ()
		{
			string s = "mono";
			IntPtr ptr = Marshal.StringToBSTR (s);
			string s2 = Marshal.PtrToStringBSTR (ptr);
			Assert.AreEqual (s, s2, "string");
		}

		[Test]
		[Category ("MobileNotWorking")]
		public void StringToBSTRWithNullValues ()
		{
			int size = 128;
			string s = String.Empty.PadLeft (size, '\0');
			Assert.AreEqual (size, s.Length, "Length-1");

			IntPtr ptr = Marshal.StringToBSTR (s);
			try {
				for (int i = 0; i < size; i += 4)
					Marshal.WriteInt32 (ptr, i, 0);

				string s2 = Marshal.PtrToStringBSTR (ptr);
				Assert.AreEqual (128, s2.Length, "Length-2");
			} finally {
				Marshal.FreeBSTR (ptr);
			}
		}

		[Test]
		public void StringToHGlobalAnsiWithNullValues ()
		{
			int size = 128;
			string s = String.Empty.PadLeft (size, '\0');
			Assert.AreEqual (size, s.Length, "Length-1");

			IntPtr ptr = Marshal.StringToHGlobalAnsi (s);
			try {
				for (int i = 0; i < size; i += 4)
					Marshal.WriteInt32 (ptr, i, 0);

				string s2 = Marshal.PtrToStringAnsi (ptr);
				Assert.AreEqual (0, s2.Length, "Length-2");
			} finally {
				Marshal.FreeHGlobal (ptr);
			}
		}

		[Test]
		public void StringToHGlobalAutoWithNullValues ()
		{
			int size = 128;
			string s = String.Empty.PadLeft (size, '\0');
			Assert.AreEqual (size, s.Length, "Length-1");

			IntPtr ptr = Marshal.StringToHGlobalAuto (s);
			try {
				for (int i = 0; i < size; i += 4)
					Marshal.WriteInt32 (ptr, i, 0);

				string s2 = Marshal.PtrToStringAuto (ptr);
				Assert.AreEqual (0, s2.Length, "Length-2");
			} finally {
				Marshal.FreeHGlobal (ptr);
			}
		}

		[Test]
		public void StringToHGlobalUniWithNullValues ()
		{
			int size = 128;
			string s = String.Empty.PadLeft (size, '\0');
			Assert.AreEqual (size, s.Length, "Length-1");

			IntPtr ptr = Marshal.StringToHGlobalUni (s);
			try {
				for (int i = 0; i < size; i += 4)
					Marshal.WriteInt32 (ptr, i, 0);

				string s2 = Marshal.PtrToStringUni (ptr);
				Assert.AreEqual (0, s2.Length, "Length-2");
			} finally {
				Marshal.FreeHGlobal (ptr);
			}
		}

		[Test]
		public void StringToCoTaskMemAnsiWithNullValues ()
		{
			int size = 128;
			string s = String.Empty.PadLeft (size, '\0');
			Assert.AreEqual (size, s.Length, "Length-1");

			IntPtr ptr = Marshal.StringToCoTaskMemAnsi (s);
			try {
				for (int i = 0; i < size; i += 4)
					Marshal.WriteInt32 (ptr, i, 0);

				string s2 = Marshal.PtrToStringAnsi (ptr);
				Assert.AreEqual (0, s2.Length, "Length-2");
			} finally {
				Marshal.FreeCoTaskMem (ptr);
			}
		}

		[Test]
		public void StringToCoTaskMemAutoWithNullValues ()
		{
			int size = 128;
			string s = String.Empty.PadLeft (size, '\0');
			Assert.AreEqual (size, s.Length, "Length-1");

			IntPtr ptr = Marshal.StringToCoTaskMemAuto (s);
			try {
				for (int i = 0; i < size; i += 4)
					Marshal.WriteInt32 (ptr, i, 0);

				string s2 = Marshal.PtrToStringAuto (ptr);
				Assert.AreEqual (0, s2.Length, "Length-2");
			} finally {
				Marshal.FreeCoTaskMem (ptr);
			}
		}

		[Test]
		public void StringToCoTaskMemUniWithNullValues ()
		{
			int size = 128;
			string s = String.Empty.PadLeft (size, '\0');
			Assert.AreEqual (size, s.Length, "Length-1");

			IntPtr ptr = Marshal.StringToCoTaskMemUni (s);
			try {
				for (int i = 0; i < size; i += 4)
					Marshal.WriteInt32 (ptr, i, 0);

				string s2 = Marshal.PtrToStringUni (ptr);
				Assert.AreEqual (0, s2.Length, "Length-2");
			} finally {
				Marshal.FreeCoTaskMem (ptr);
			}
		}
		private const string NotSupported = "Not supported before Windows 2000 Service Pack 3";
		private static char[] PlainText = new char[] { 'a', 'b', 'c' };
		private static byte[] AsciiPlainText = new byte[] { (byte) 'a', (byte) 'b', (byte) 'c' };

		private unsafe SecureString GetSecureString ()
		{
			fixed (char* p = &PlainText[0]) {
				return new SecureString (p, PlainText.Length);
			}
		}

		[Test]
		public void SecureStringToBSTR_Null ()
		{
			try {
				Marshal.SecureStringToBSTR (null);
				Assert.Fail ("#1");
			} catch (ArgumentNullException ex) {
				Assert.AreEqual (typeof (ArgumentNullException), ex.GetType (), "#2");
				Assert.IsNull (ex.InnerException, "#3");
				Assert.IsNotNull (ex.Message, "#4");
				Assert.AreEqual ("s", ex.ParamName, "#5");
			}
		}

		[Test]
		public void SecureStringToBSTR ()
		{
			try {
				SecureString ss = GetSecureString ();
				IntPtr p = Marshal.SecureStringToBSTR (ss);

				char[] decrypted = new char[ss.Length];
				Marshal.Copy (p, decrypted, 0, decrypted.Length);
				Assert.AreEqual (PlainText, decrypted, "Decrypted");

				Marshal.ZeroFreeBSTR (p);
			} catch (NotSupportedException) {
				Assert.Ignore (NotSupported);
			}
		}

		[Test]
		public void SecureStringToCoTaskMemAnsi_Null ()
		{
			try {
				Marshal.SecureStringToCoTaskMemAnsi (null);
				Assert.Fail ("#1");
			} catch (ArgumentNullException ex) {
				Assert.AreEqual (typeof (ArgumentNullException), ex.GetType (), "#2");
				Assert.IsNull (ex.InnerException, "#3");
				Assert.IsNotNull (ex.Message, "#4");
				Assert.AreEqual ("s", ex.ParamName, "#5");
			}
		}

		[Test]
		public void SecureStringToCoTaskMemAnsi ()
		{
			try {
				SecureString ss = GetSecureString ();
				IntPtr p = Marshal.SecureStringToCoTaskMemAnsi (ss);

				byte[] decrypted = new byte[ss.Length];
				Marshal.Copy (p, decrypted, 0, decrypted.Length);
				Assert.AreEqual (AsciiPlainText, decrypted, "Decrypted");

				Marshal.ZeroFreeCoTaskMemAnsi (p);
			} catch (NotSupportedException) {
				Assert.Ignore (NotSupported);
			}
		}

		[Test]
		public void SecureStringToCoTaskMemUnicode_Null ()
		{
			try {
				Marshal.SecureStringToCoTaskMemUnicode (null);
				Assert.Fail ("#1");
			} catch (ArgumentNullException ex) {
				Assert.AreEqual (typeof (ArgumentNullException), ex.GetType (), "#2");
				Assert.IsNull (ex.InnerException, "#3");
				Assert.IsNotNull (ex.Message, "#4");
				Assert.AreEqual ("s", ex.ParamName, "#5");
			}
		}

		[Test]
		public void SecureStringToCoTaskMemUnicode ()
		{
			try {
				SecureString ss = GetSecureString ();
				IntPtr p = Marshal.SecureStringToCoTaskMemUnicode (ss);

				char[] decrypted = new char[ss.Length];
				Marshal.Copy (p, decrypted, 0, decrypted.Length);
				Assert.AreEqual (PlainText, decrypted, "Decrypted");

				Marshal.ZeroFreeCoTaskMemUnicode (p);
			} catch (NotSupportedException) {
				Assert.Ignore (NotSupported);
			}
		}

		[Test]
		public void SecureStringToGlobalAllocAnsi_Null ()
		{
			try {
				Marshal.SecureStringToGlobalAllocAnsi (null);
				Assert.Fail ("#1");
			} catch (ArgumentNullException ex) {
				Assert.AreEqual (typeof (ArgumentNullException), ex.GetType (), "#2");
				Assert.IsNull (ex.InnerException, "#3");
				Assert.IsNotNull (ex.Message, "#4");
				Assert.AreEqual ("s", ex.ParamName, "#5");
			}
		}

		[Test]
		public void SecureStringToGlobalAllocAnsi ()
		{
			try {
				SecureString ss = GetSecureString ();
				IntPtr p = Marshal.SecureStringToGlobalAllocAnsi (ss);

				byte[] decrypted = new byte[ss.Length];
				Marshal.Copy (p, decrypted, 0, decrypted.Length);
				Assert.AreEqual (AsciiPlainText, decrypted, "Decrypted");

				Marshal.ZeroFreeGlobalAllocAnsi (p);
			} catch (NotSupportedException) {
				Assert.Ignore (NotSupported);
			}
		}

		[Test]
		public void SecureStringToGlobalAllocUnicode_Null ()
		{
			try {
				Marshal.SecureStringToGlobalAllocUnicode (null);
				Assert.Fail ("#1");
			} catch (ArgumentNullException ex) {
				Assert.AreEqual (typeof (ArgumentNullException), ex.GetType (), "#2");
				Assert.IsNull (ex.InnerException, "#3");
				Assert.IsNotNull (ex.Message, "#4");
				Assert.AreEqual ("s", ex.ParamName, "#5");
			}
		}

		[Test]
		public void SecureStringToGlobalAllocUnicode ()
		{
			try {
				SecureString ss = GetSecureString ();
				IntPtr p = Marshal.SecureStringToGlobalAllocUnicode (ss);

				char[] decrypted = new char[ss.Length];
				Marshal.Copy (p, decrypted, 0, decrypted.Length);
				Assert.AreEqual (PlainText, decrypted, "Decrypted");

				Marshal.ZeroFreeGlobalAllocUnicode (p);
			} catch (NotSupportedException) {
				Assert.Ignore (NotSupported);
			}
		}

#if !MOBILE
		[Test]
		public void TestGetComSlotForMethodInfo ()
		{
			Assert.AreEqual (7, Marshal.GetComSlotForMethodInfo(typeof(ITestDefault).GetMethod("DoNothing")));
			Assert.AreEqual (7, Marshal.GetComSlotForMethodInfo(typeof(ITestDual).GetMethod("DoNothing")));
			Assert.AreEqual (7, Marshal.GetComSlotForMethodInfo (typeof(ITestDefault).GetMethod ("DoNothing")));
			Assert.AreEqual (3, Marshal.GetComSlotForMethodInfo (typeof(ITestUnknown).GetMethod ("DoNothing")));

			for (int i = 0; i < 10; i++)
				Assert.AreEqual (7+i, Marshal.GetComSlotForMethodInfo(typeof(ITestInterface).GetMethod ("Method"+i.ToString())));
		}

		[Test]
		public void TestGetComSlotForMethod_Method_Null ()
		{
			try {
				Marshal.GetComSlotForMethodInfo (null);
				Assert.Fail ("#1");
			} catch (ArgumentNullException ex) {
				Assert.AreEqual (typeof (ArgumentNullException), ex.GetType (), "#2");
				Assert.IsNull (ex.InnerException, "#3");
				Assert.IsNotNull (ex.Message, "#4");
				Assert.AreEqual ("m", ex.ParamName, "#5");
			}
		}

		[Test]
		public void TestGetComSlotForMethodInfo_Method_NotOnInterface ()
		{
			MethodInfo m = typeof(TestCoClass).GetMethod ("DoNothing");
			try {
				Marshal.GetComSlotForMethodInfo (m);
				Assert.Fail ("#1");
			} catch (ArgumentException ex) {
				// The MemberInfo must be an interface method
				Assert.AreEqual (typeof (ArgumentException), ex.GetType (), "#2");
				Assert.IsNull (ex.InnerException, "#3");
				Assert.IsNotNull (ex.Message, "#4");
				Assert.AreEqual ("m", ex.ParamName, "#5");
			}
		}
#endif
		[Test]
		public void TestPtrToStringAuto ()
		{
			string input = Guid.NewGuid ().ToString ();
			string output;
			string output2;
			int len = 4;
			IntPtr ptr;

			if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
				// Auto -> Uni
				ptr = Marshal.StringToHGlobalAuto (input);
				output = Marshal.PtrToStringUni (ptr);
				output2 = Marshal.PtrToStringUni (ptr, len);
			} else {
				// Auto -> Ansi
				ptr = Marshal.StringToHGlobalAuto (input);
				output = Marshal.PtrToStringAnsi (ptr);
				output2 = Marshal.PtrToStringAnsi (ptr, len);
			}

			try {
				Assert.AreEqual (input, output, "#1");
				Assert.AreEqual (input.Substring (0, len), output2, "#2");
			} finally {
				Marshal.FreeHGlobal (ptr);
			}
		}
#if !MOBILE
		[Test]
		public void TestGenerateProgIdForType()
		{
			string output;
			
			output = Marshal.GenerateProgIdForType(typeof(TestCoClass));
			Assert.AreEqual ("MonoTests.System.Runtime.InteropServices.TestCoClass", output, "#1");
			
			output = Marshal.GenerateProgIdForType(typeof(TestCoClassWithProgId));
			Assert.AreEqual ("CoClassWithProgId", output, "#2");
		}
#endif
		[Test]
		public void TestGlobalAlloc ()
		{
			IntPtr mem = Marshal.AllocHGlobal (100);
			mem = Marshal.ReAllocHGlobal (mem, (IntPtr) 1000000);
			Marshal.FreeHGlobal (mem);
		}
		
		[Test]
		public void FreeHGlobal ()
		{
			// clear user doubts on assistly #6749
			for (int i = 0; i < 1024; i++) {
				IntPtr p = Marshal.AllocHGlobal (1024 * 1024);
				Assert.AreNotEqual (IntPtr.Zero, p, i.ToString ());
				Marshal.FreeHGlobal (p);
			}
		}

		[StructLayout (LayoutKind.Sequential)]
		public struct SimpleStruct2 {
			public int a;
			public int b;
		}

		[Test]
		public void PtrToStructureNull ()
		{
			Assert.IsNull (Marshal.PtrToStructure (IntPtr.Zero, typeof (SimpleStruct2)));
		}
		
		[Test]
		public void TestGetExceptionForHR ()
		{
			const int E_OUTOFMEMORY = unchecked ((int) 0x8007000E);
			const int E_INVALIDARG = unchecked ((int) 0X80070057);
			
			Exception ex = Marshal.GetExceptionForHR (E_OUTOFMEMORY);
			Assert.AreEqual (typeof (OutOfMemoryException), ex.GetType (), "E_OUTOFMEMORY");
			
			ex = Marshal.GetExceptionForHR (E_INVALIDARG);
			Assert.AreEqual (typeof (ArgumentException), ex.GetType (), "E_INVALIDARG");
		}
		bool RunningOnMono {
			get {
				return (Type.GetType ("System.MonoType", false) != null);
			}
		}

#if !MOBILE
		[DllImport ("kernel32.dll", SetLastError = true)]
		[PreserveSig]
		static extern uint GetModuleFileName (
			[In]
			IntPtr hModule,
			[Out]
			StringBuilder lpFilename,
			[In]
			[MarshalAs (UnmanagedType.U4)]
			int nSize
		);
#endif

#if !MOBILE_STATIC
		[StructLayout( LayoutKind.Sequential, Pack = 1 )]
		public class FourByteStruct
		{
			public UInt16 value1;
			public UInt16 value2;
		}

		[StructLayout( LayoutKind.Sequential, Pack = 1 )]
		public class ByteArrayFourByteStruct : FourByteStruct
		{
			[MarshalAs( UnmanagedType.ByValArray, SizeConst = 5 )]
			public byte[] array;
		}

		[StructLayout( LayoutKind.Sequential, Pack = 1 )]
		public class SingleByteStruct
		{
			public byte value1;
		}

		[StructLayout( LayoutKind.Sequential, Pack = 1 )]
		public class ByteArraySingleByteStruct : SingleByteStruct
		{
			[MarshalAs( UnmanagedType.ByValArray, SizeConst = 5 )]
			public byte[] array1;
			public byte value2;
		}

		[StructLayout( LayoutKind.Sequential, Pack = 1 )]
		public class ByteArraySingleByteChildStruct : ByteArraySingleByteStruct
		{
			[MarshalAs( UnmanagedType.ByValArray, SizeConst = 5 )]
			public byte[] array2;
		}

		[Test]
		public void CheckByteArrayFourByteStruct()
		{
			ByteArrayFourByteStruct myStruct = new ByteArrayFourByteStruct
			{ value1 = 42, value2 = 53, array = Encoding.UTF8.GetBytes( "Hello" ) };

			byte[] buffer = Serialize (myStruct);

			UInt16 value1 = BitConverter.ToUInt16 (buffer, 0);
			UInt16 value2 = BitConverter.ToUInt16 (buffer, 2);
			string array = Encoding.UTF8.GetString (buffer, 4, 5);

			Assert.AreEqual((UInt16)42, value1);
			Assert.AreEqual((UInt16)53, value2);
			Assert.AreEqual ("Hello", array);
		}

		[Test]
		public void CheckByteArraySingleByteChildStruct()
		{
			ByteArraySingleByteChildStruct myStruct = new ByteArraySingleByteChildStruct
			{ value1 = 42, array1 = Encoding.UTF8.GetBytes( "Hello" ), value2 = 53,  array2 = Encoding.UTF8.GetBytes( "World" ) };

			byte[] array = Serialize (myStruct);

			byte value1 = array [0];
			string array1 = Encoding.UTF8.GetString (array, 1, 5);
			byte value2 = array [6];
			string array2 = Encoding.UTF8.GetString (array, 7, 5);

			Assert.AreEqual((byte)42, value1);
			Assert.AreEqual ("Hello", array1);
			Assert.AreEqual((byte)53, value2);
			Assert.AreEqual ("World", array2);
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct FiveByteStruct
		{
			public uint uIntField;
			public byte byteField;
		};

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public class Base
		{
			public ushort firstUShortField;
			public ushort secondUShortField;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public class Derived : Base
		{
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
			public FiveByteStruct[] arrayField;
		}

		[Test]
		public void CheckPtrToStructureWithFixedArrayAndBaseClassFields()
		{
			const int arraySize = 6;
			var derived = new Derived
			{
				arrayField = new FiveByteStruct[arraySize],
				firstUShortField = 42,
				secondUShortField = 43
			};

			for (var i = 0; i < arraySize; ++i)
			{
				derived.arrayField[i].byteField = (byte)i;
				derived.arrayField[i].uIntField = (uint)i * 10;
			}

			var array = Serialize(derived);
			var deserializedDerived = Deserialize<Derived>(array);

			Assert.AreEqual(derived.firstUShortField, deserializedDerived.firstUShortField, "The firstUShortField differs, which is not expected.");
			Assert.AreEqual(derived.secondUShortField, deserializedDerived.secondUShortField, "The secondUShortField differs, which is not expected.");

			for (var i = 0; i < arraySize; ++i)
			{
				Assert.AreEqual(derived.arrayField[i].byteField, deserializedDerived.arrayField[i].byteField, string.Format("The byteField at index {0} differs, which is not expected.", i));
				Assert.AreEqual(derived.arrayField[i].uIntField, deserializedDerived.arrayField[i].uIntField, string.Format("The uIntField at index {0} differs, which is not expected.", i));
			}
		}

		public static byte[] Serialize( object obj )
		{
			int nTypeSize = Marshal.SizeOf( obj );
			byte[] arrBuffer = new byte[nTypeSize];

			GCHandle hGCHandle = GCHandle.Alloc( arrBuffer, GCHandleType.Pinned );
			IntPtr pBuffer = hGCHandle.AddrOfPinnedObject();
			Marshal.StructureToPtr( obj, pBuffer, false );
			hGCHandle.Free();

			return arrBuffer;
		}

		public static T Deserialize<T>(byte[] buffer)
		{
			var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
			var pBuffer = handle.AddrOfPinnedObject();
			var objResult = (T)Marshal.PtrToStructure(pBuffer, typeof(T));
			handle.Free();

			return objResult;
		}
#endif
	}
#if !MOBILE
	[ComImport()]
	[Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA")]
	interface ITestDefault
	{
		void DoNothing ();
	}

	[ComImport()]
	[Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA")]
	[InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
	interface ITestDispatch
	{
		void DoNothing ();
	}

	[ComImport()]
	[Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA")]
	[InterfaceType(ComInterfaceType.InterfaceIsDual)]
	interface ITestDual
	{
		void DoNothing ();
	}

	[ComImport()]
	[Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	interface ITestUnknown
	{
		void DoNothing ();
	}

	[ComImport()]
	[Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA")]
	interface ITestInterface
	{
		void Method0 ();
		void Method1 ();
		void Method2 ();
		void Method3 ();
		void Method4 ();
		void Method5 ();
		void Method6 ();
		void Method7 ();
		void Method8 ();
		void Method9 ();
	}

	public class TestCoClass : ITestDispatch
	{
		public void DoNothing ()
		{
		}
	}

	[ProgId("CoClassWithProgId")]
	public class TestCoClassWithProgId : ITestDispatch
	{
		public void DoNothing ()
		{
		}
	}
#endif
}
