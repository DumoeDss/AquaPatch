
using AquaSys.Tools;

namespace YooAsset
{
	internal class QueryBuildinPackageVersionOperation : AsyncOperationBase
	{
		private enum ESteps
		{
			None,
			LoadBuildinPackageVersionFile,
			Done,
		}

		private readonly string _packageName;
		private readonly string _packageVersion;
		private UnityWebDataRequester _downloader;
		private ESteps _steps = ESteps.None;

		/// <summary>
		/// 包裹版本
		/// </summary>
		public YooAssetVersion PackageVersion { private set; get; }


		public QueryBuildinPackageVersionOperation(string packageName,string packageVersion)
		{
			_packageName = packageName;
			_packageVersion = packageVersion;
		}
		internal override void Start()
		{
			_steps = ESteps.LoadBuildinPackageVersionFile;
		}
		internal override void Update()
		{
			if (_steps == ESteps.None || _steps == ESteps.Done)
				return;

			if (_steps == ESteps.LoadBuildinPackageVersionFile)
			{
				if (_downloader == null)
				{
					string fileName = YooAssetSettingsData.GetPackageVersionFileName(_packageName, _packageVersion);
					string filePath = PathHelper.MakeStreamingLoadPath(fileName, _packageName);
					string url = PathHelper.ConvertToWWWPath(filePath);
					_downloader = new UnityWebDataRequester();
					_downloader.SendRequest(url);
				}

				if (_downloader.IsDone() == false)
					return;

				if (_downloader.HasError())
				{
					_steps = ESteps.Done;
					Status = EOperationStatus.Failed;
					Error = _downloader.GetError();
				}
				else
				{
					PackageVersion = StreamTools.DeserializeObject<YooAssetVersion>(_downloader.GetText());
					if (PackageVersion==null)
					{
						_steps = ESteps.Done;
						Status = EOperationStatus.Failed;
						Error = $"Buildin package version file content is empty !";
					}
					else
					{
						_steps = ESteps.Done;
						Status = EOperationStatus.Succeed;
					}
				}

				_downloader.Dispose();
			}
		}
	}
}