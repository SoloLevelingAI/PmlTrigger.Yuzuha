/** Client-neutral document contract. No runtime or UI framework imports. */
export interface ImageAsset { mediaType: 'image/png' | 'image/jpeg'; base64: string }
export interface RibbonButton {
  sourceId?: string;
  id: string;
  caption: string;
  macro: string;
  commandKey: string;
  image: ImageAsset | null;
  tooltipText: string;
  tooltipTitle: string;
}
export interface RibbonItem { buttonId: string; size: 'large' | 'small' }
export interface RibbonGroup { id: string; caption: string; items: RibbonItem[]; sourceId?: string }
export interface RibbonTab { id: string; caption: string; groups: RibbonGroup[]; sourceId?: string }
export interface RibbonProject {
  preferredBinding?: { monPath: string; manifestName: string; entryName: string };
  documentId?: string;
  origin?: { fileName: string; xml: string; sourcePath?: string; sourceHash: string };
  schemaVersion: 1;
  namespace: string;
  buttons: RibbonButton[];
  tabs: RibbonTab[];
}
export interface Issue { code: string; path: string; message: string }
export interface Validation { valid: boolean; issues: Issue[] }
export interface DeploymentTarget { monPath: string; directory: string; manifests: string[]; label: string }
export interface DeploymentPlan {
  monPath: string; manifestPath: string; uicPath: string; entryName: string; registration: string;
  manifestHash: string; uicHash: string | null; nextManifestHash: string; nextUicHash: string;
  uicBytes: number; replacesUic: boolean; unchanged: boolean;
}
export const LIMITS = { imageBytes: 12 * 1024 * 1024, imageDimension: 8192, requestBytes: 20 * 1024 * 1024 };
