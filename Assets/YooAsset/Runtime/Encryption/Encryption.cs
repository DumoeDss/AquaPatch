using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

namespace AquaSys.Patch.Encryption
{
	public class EncryptionNone : IEncryptionServices
	{
		public EncryptResult Encrypt(EncryptFileInfo fileInfo)
		{
			EncryptResult result = new EncryptResult();
			result.LoadMethod = EBundleLoadMethod.Normal;
			return result;
		}
	}
}