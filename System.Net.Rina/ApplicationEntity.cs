//
//  ApplicationEntity.cs
//
//  Author:
//       Ondrej Rysavy <rysavy@fit.vutbr.cz>
//
//  Copyright (c) 2014 PRISTINE
//
//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation; either version 2 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
//
using System;
using System.Threading;
namespace System.Net.Rina
{
	/// <summary>
	/// Application entity is a description of application that resides in RINA DIF.
	/// </summary>
	public abstract class ApplicationEntity : IDisposable
	{
		public ApplicationNamingInfo NamingInfo { get; private set; }


		public ApplicationEntity(String apName, String apInstance, String aeName, String aeInstance)
		{
			this.NamingInfo = new ApplicationNamingInfo (apName, apInstance, aeName, aeInstance);
		}

		/// <summary>
		/// This method is called before a new thread for application entity is created to initialize instance.
		/// </summary>
		protected abstract bool Initialize();

		/// <summary>
		/// This method is executed when system runs an instance of the application entity.
		/// </summary>
		protected abstract void Run();


		void threadWorker()
		{
			if (this.Initialize ()) {
				try {
					this.Run ();
				} finally {
					this.Dispose ();
				}
			}
		}

		/// <summary>
		/// Creates the thread for the provided application entity.
		/// </summary>
		/// <returns>The thread object</returns>
		/// <param name="ae">Ae.</param>
		/// <remarks>
		/// To execute, pause or stop the thread, just use the following code snippet:
		/// thread.Start();			// starts thread
		/// ...
		/// thread.Sleep(1000);   	// pause thread for 1s
		/// ...
		/// thread.Abort();  		// request thread to stop
		/// ...
		/// thread.Join();   		// wait until the thread finishes
		/// </remarks>
		public static Thread CreateThread(ApplicationEntity ae)
		{
			ae.Initialize ();
			Thread aeThread = new Thread(new ThreadStart(ae.threadWorker));
			return aeThread;
		}

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).          
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources. 
        // ~ApplicationEntity() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}

