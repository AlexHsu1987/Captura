// Adapted from https://github.com/jasonpang/desktop-duplication-net

using Screna;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Captura;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace DesktopDuplication
{
    public class DesktopDuplicator : IDisposable
    {
        readonly Device _device;
        readonly Texture2D _desktopImageTexture;
        readonly Rectangle _rect;
        readonly bool _includeCursor;
        readonly Output1 _output;
        DuplCapture _duplCapture;
        readonly ImagePool _imagePool;

        public DesktopDuplicator(Rectangle Rect, bool IncludeCursor, Adapter Adapter, Output1 Output)
        {
            _rect = Rect;
            _includeCursor = IncludeCursor;
            _output = Output;

            _imagePool = new ImagePool(Rect.Width, Rect.Height);
            
            _device = new Device(Adapter);

            var textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.Read,
                BindFlags = BindFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                Width = _rect.Width,
                Height = _rect.Height,
                OptionFlags = ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = ResourceUsage.Staging
            };

            InitDuplCapture();

            _desktopImageTexture = new Texture2D(_device, textureDesc);
        }

        void InitDuplCapture()
        {
            _duplCapture?.Dispose();
            
            _duplCapture = new DuplCapture(_device, _output);
        }

        public IBitmapFrame Capture()
        {
            try
            {
                if (!_duplCapture.Get(_desktopImageTexture))
                    return RepeatFrame.Instance;
            }
            catch
            {
                try { InitDuplCapture(); }
                catch
                {
                    // catch
                }

                return RepeatFrame.Instance;
            }

            var mapSource = _device.ImmediateContext.MapSubresource(_desktopImageTexture, 0, MapMode.Read, MapFlags.None);

            try
            {
                return ProcessFrame(mapSource.DataPointer);
            }
            finally
            {
                _device.ImmediateContext.UnmapSubresource(_desktopImageTexture, 0);
            }
        }

        IBitmapFrame ProcessFrame(IntPtr SourcePtr)
        {
            var img = _imagePool.Get();
            
            Marshal.Copy(SourcePtr, img.ImageData, 0, _rect.Width * _rect.Height * 4);

            //if (_includeCursor && (_frameInfo.LastMouseUpdateTime == 0 || _frameInfo.PointerPosition.Visible))
            //{
            //    using (var g = Graphics.FromImage(frame))
            //        MouseCursor.Draw(g, P => new Point(P.X - _rect.X, P.Y - _rect.Y));
            //}

            return img;
        }

        public void Dispose()
        {
            try
            {
                _duplCapture?.Dispose();
                _desktopImageTexture?.Dispose();
                _device?.Dispose();
            }
            catch { }
        }
    }
}
