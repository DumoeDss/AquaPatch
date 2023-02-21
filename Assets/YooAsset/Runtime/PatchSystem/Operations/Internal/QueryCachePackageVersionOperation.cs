using AquaSys.Tools;
using System.IO;

namespace YooAsset
{
	internal class QueryCachePackageVersionOperation : AsyncOperationBase
	{
		private enum ESteps
		{
			None,
			LoadCachePackageVersionFile,
			Done,
		}

		private readonly string _packageName;
		private readonly string _packageVersion;
		private ESteps _steps = ESteps.None;

		/// <summary>
		/// 包裹版本
		/// </summary>
		public YooAssetVersion PackageVersion { private set; get; }


		public QueryCachePackageVersionOperation(string packageName,string packageVersion)
		{
			_packageName = packageName;
			_packageVersion = packageVersion;
		}
		internal override void Start()
		{
			_steps = ESteps.LoadCachePackageVersionFile;
		}
		internal override void Update()
		{
			if (_steps == ESteps.None || _steps == ESteps.Done)
				return;

			if (_steps == ESteps.LoadCachePackageVersionFile)
			{
				string filePath = PersistentHelper.GetCachePackageVersionFilePath(_packageName, _packageVersion);
				if (File.Exists(filePath) == false)
				{
					_steps = ESteps.Done;
					Status = EOperationStatus.Failed;
					Error = $"Cache package version file not found : {filePath}";
					return;
				}

				PackageVersion = StreamTools.DeserializeObjectFromFilePath<YooAssetVersion>(filePath);
				if (PackageVersion==null)
				{
					_steps = ESteps.Done;
					Status = EOperationStatus.Failed;
					Error = $"Cache package version file content is empty !";
				}
				else
				{
					_steps = ESteps.Done;
					Status = EOperationStatus.Succeed;
				}
			}
		}
	}
}