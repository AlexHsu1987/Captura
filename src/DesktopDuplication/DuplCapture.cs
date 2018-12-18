using System;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace DesktopDuplication
{
    class DuplCapture : IDisposable
    {
        readonly Device _device;
        readonly OutputDuplication _deskDupl;

        SharpDX.DXGI.Resource _desktopResource;
        Task _acquireTask;
        const int Timeout = 5000;
        OutputDuplicateFrameInformation _frameInfo;

        // ReSharper disable once SuggestBaseTypeForParameter
        public DuplCapture(Device Device, Output1 Output)
        {
            _device = Device;

            try
            {
                _deskDupl = Output.DuplicateOutput(Device);
            }
            catch (SharpDXException e) when (e.Descriptor == SharpDX.DXGI.ResultCode.NotCurrentlyAvailable)
            {
                throw new Exception("There is already the maximum number of applications using the Desktop Duplication API running, please close one of the applications and try again.", e);
            }
            catch (SharpDXException e) when (e.Descriptor == SharpDX.DXGI.ResultCode.Unsupported)
            {
                throw new NotSupportedException("Desktop Duplication is not supported on this system.\nIf you have multiple graphic cards, try running Captura on integrated graphics.", e);
            }
        }

        void BeginAcquireTask()
        {
            _acquireTask = Task.Run(() => _deskDupl.AcquireNextFrame(Timeout, out _frameInfo, out _desktopResource));
        }

        public bool Get(Texture2D Texture)
        {
            if (_acquireTask == null)
            {
                BeginAcquireTask();

                return false;
            }

            try
            {
                _acquireTask.GetAwaiter().GetResult();
            }
            catch (SharpDXException e) when (e.Descriptor == SharpDX.DXGI.ResultCode.WaitTimeout)
            {
                return false;
            }
            catch (SharpDXException e) when (e.ResultCode.Failure)
            {
                throw new Exception("Failed to acquire next frame.", e);
            }

            using (_desktopResource)
            {
                using (var tempTexture = _desktopResource.QueryInterface<Texture2D>())
                {
                    _device.ImmediateContext.CopyResource(tempTexture, Texture);
                }
            }

            ReleaseFrame();
            BeginAcquireTask();

            return true;
        }

        void ReleaseFrame()
        {
            try
            {
                _deskDupl.ReleaseFrame();
            }
            catch (SharpDXException e)
            {
                if (e.ResultCode.Failure)
                {
                    throw new Exception("Failed to release frame.", e);
                }
            }
        }

        public void Dispose()
        {
            try { _acquireTask?.GetAwaiter().GetResult(); }
            catch { }

            try
            {
                _deskDupl.Dispose();
            }
            catch { }
        }
    }
}