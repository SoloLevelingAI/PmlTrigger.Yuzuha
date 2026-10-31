import sharp from 'sharp';
import { LIMITS, type ImageAsset } from '../contracts/index.ts';

export async function imageProblem(image: unknown): Promise<string | null> {
  if (!image || typeof image !== 'object') return '请选择 PNG 或 JPEG 图片';
  const value = image as ImageAsset;
  if (!['image/png', 'image/jpeg'].includes(value.mediaType) || typeof value.base64 !== 'string') return '图片必须是 PNG 或 JPEG';
  if (!value.base64.length || value.base64.length > Math.ceil(LIMITS.imageBytes / 3) * 4) return '图片为空或超过 12 MiB';
  if (!/^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/.test(value.base64)) return '图片 Base64 无效';
  const bytes = Buffer.from(value.base64, 'base64');
  if (bytes.length > LIMITS.imageBytes) return '图片不能超过 12 MiB';
  try {
    const decoder = sharp(bytes, { limitInputPixels: LIMITS.imageDimension ** 2, failOn: 'warning' });
    const m = await decoder.metadata();
    if (!m.width || !m.height || m.width > LIMITS.imageDimension || m.height > LIMITS.imageDimension || (m.pages ?? 1) > 1) return '图片尺寸不能超过 8192×8192，且需要单帧图片';
    if ((m.format === 'png' ? 'image/png' : m.format === 'jpeg' ? 'image/jpeg' : '') !== value.mediaType) return '图片内容与 PNG/JPEG 类型不匹配';
    await decoder.raw().toBuffer();
    return null;
  } catch { return '图片损坏、无法完整解码或尺寸超限'; }
}

/** A transparent square canvas keeps aspect ratio; no source metadata is retained. */
export async function resizeIcon(image: ImageAsset, size: 16 | 32): Promise<ImageAsset> {
  const bytes = await sharp(Buffer.from(image.base64, 'base64'), {
    limitInputPixels: LIMITS.imageDimension ** 2, failOn: 'warning'
  }).rotate().resize(size, size, { fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .png({ compressionLevel: 9, adaptiveFiltering: true }).toBuffer();
  return { mediaType: 'image/png', base64: bytes.toString('base64') };
}
