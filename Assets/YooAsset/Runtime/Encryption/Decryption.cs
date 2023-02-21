using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

using YooAsset;

namespace AquaSys.Patch.Encryption
{
	public class DecryptionServices : IDecryptionServices
	{
		public ulong LoadFromFileOffset(DecryptFileInfo fileInfo)
		{
			return 32;
		}

		public byte[] LoadFromMemory(DecryptFileInfo fileInfo)
		{
			return null;
		}

		public FileStream LoadFromStream(DecryptFileInfo fileInfo)
		{
			FileStream bundleStream = new FileStream(fileInfo.FilePath, FileMode.Open);
			return bundleStream;
		}

		public uint GetManagedReadBufferSize()
		{
			return 1024;
		}
	}
}
