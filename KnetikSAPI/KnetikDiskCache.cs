using UnityEngine;
using System.Collections;
using System.IO;
using System;
using KnetikHTTP;

// Used to store and fetch items onto Disk Cache

namespace KnetikHTTP
{
	public class KnetikDiskCacheOperation
	{
		public bool isDone = false;
		public bool fromCache = false;
		public KnetikRequest request = null;
	}

#if UNITY_WEBPLAYER
	public class KnetikDiskCache : MonoBehaviour
	{
		static KnetikDiskCache _instance = null;

		public static KnetikDiskCache Instance 
		{
			get {

				if (_instance == null) 
				{
					var g = new GameObject ("DiskCache", typeof(KnetikDiskCache));
					g.hideFlags = HideFlags.HideAndDontSave;
					_instance = g.GetComponent<KnetikDiskCache> ();
				}

				return _instance;
			}
		}

		public DiskCacheOperation Fetch (Request request)
		{
			var handle = new DiskCacheOperation ();
			handle.request = request;
			StartCoroutine (Download (request, handle));
			return handle;
		}

		IEnumerator Download(Request request, DiskCacheOperation handle)
		{
			request.Send ();

			while (!request.isDone)
			{
				yield return new WaitForEndOfFrame ();
			}

			handle.isDone = true;
		}
	}
#else
	public class KnetikDiskCache : MonoBehaviour
	{
		string cachePath = null;
		static KnetikDiskCache _instance = null;

		public static KnetikDiskCache Instance 
		{
			get {

				if (_instance == null) 
				{
					var g = new GameObject ("DiskCache", typeof(KnetikDiskCache));
					g.hideFlags = HideFlags.HideAndDontSave;
					_instance = g.GetComponent<KnetikDiskCache> ();
				}

				return _instance;
			}
		}

		void Awake ()
		{
			cachePath = System.IO.Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "uwcache");

			if (!Directory.Exists (cachePath)) 
			{
				Directory.CreateDirectory (cachePath);
			}
		}

		public KnetikDiskCacheOperation Fetch (KnetikRequest request)
		{
			var guid = "";
            // MD5 is disposable
            // http://msdn.microsoft.com/en-us/library/system.security.cryptography.md5.aspx#3
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create ()) 
			{

                foreach (var b in md5.ComputeHash (System.Text.ASCIIEncoding.ASCII.GetBytes (request.uri.ToString ()))) 
				{
                    guid = guid + b.ToString ("X2");
                }

            }

			var filename = System.IO.Path.Combine (cachePath, guid);

			if (File.Exists (filename) && File.Exists (filename + ".etag")) 
			{
				request.SetHeader ("If-None-Match", File.ReadAllText (filename + ".etag"));
			}

			var handle = new KnetikDiskCacheOperation ();
			handle.request = request;
			StartCoroutine (DownloadAndSave (request, filename, handle));
			return handle;
		}

		IEnumerator DownloadAndSave (KnetikRequest request, string filename, KnetikDiskCacheOperation handle)
		{
			var useCachedVersion = File.Exists(filename);
			Action< KnetikHTTP.KnetikRequest > callback = request.completedCallback;
			request.Send(); // will clear the completedCallback

			while (!request.isDone) 
			{
				yield return new WaitForEndOfFrame ();
			}

			if (request.exception == null && request.response != null) 
			{

				if (request.response.status == 200) 
				{

					var etag = request.response.GetHeader ("etag");

					if (etag != "") 
					{
						File.WriteAllBytes (filename, request.response.bytes);
						File.WriteAllText (filename + ".etag", etag);
					}

					useCachedVersion = false;

				}

			}

			if(useCachedVersion) 
			{

				if(request.exception != null) 
				{
					Debug.LogWarning("Knetik Labs SDK: Using cached version due to exception");
					Debug.LogException(request.exception);
					request.exception = null;
				}

				request.response.status = 304;
				request.response.bytes = File.ReadAllBytes (filename);
				request.isDone = true;
			}
			handle.isDone = true;

            if ( callback != null )
            {
                callback( request );
            }
		}

	}
#endif
}
