import type { FrozenDeploymentPlan, FrozenDeploymentReceipt, RibbonProject } from '../../../packages/contracts/index.ts';

export const activationMessage = '需重新进入对应模块或重启 AVEVA 后检查；实际 Ribbon 加载未核验。';
type StateStore = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>;
const pendingKey = 'ribbon-pending-deployment-v2';
function browserStore(): StateStore | null { try { return globalThis.localStorage ?? null; } catch { return null; } }
function browserLocks(): Pick<LockManager, 'request'> | null { try { return globalThis.navigator?.locks ?? null; } catch { return null; } }
let unknownPlanId: string | null = null;
function pendingId(storage: StateStore | null): string | null {
  try { const raw = storage?.getItem(pendingKey); if (raw) { try { return JSON.parse(raw).planId || 'pending-record-unreadable'; } catch { return 'pending-record-unreadable'; } } } catch { return 'pending-record-unreadable'; }
  return null;
}
export async function acknowledgeManualDeploymentCheck(expectedId = pendingId(browserStore()) ?? unknownPlanId) {
  const locks = browserLocks(); if (!locks) throw new Error('缺少跨页面部署锁，不能解除暂停');
  await locks.request('ribbon-deployment-write', { mode: 'exclusive', ifAvailable: true }, async lock => {
    if (!lock) throw new Error('原部署仍在执行，请等待回执或确认进程已结束后再核对');
    const store = browserStore(); if (!store) throw new Error('无法访问部署状态存储，不能解除暂停');
    if ((pendingId(store) ?? unknownPlanId) !== expectedId) throw new Error('待核对计划已变化，请重新查看原计划编号');
    store.removeItem(pendingKey); if (store.getItem(pendingKey)) throw new Error('部署状态未清除，继续暂停');
    unknownPlanId = null;
  });
}
export class DeploymentRejected extends Error {}
export class DeploymentUnknown extends Error {
  constructor(readonly planId: string) { super(`结果不确定 · 计划 ${planId}。请核对工作区历史、目标文件与备份；不要重试或重新预览。所有部署页已暂停写入，关闭页面不会解除。`); }
}
/** One explicit preview/confirmation at a time. Unknown retains the original reference. */
export class FrozenDeploymentClient {
  private active: { plan: FrozenDeploymentPlan; kind: 'file' | 'registration' } | null = null;
  get unknownPlanId() { return pendingId(this.storage) ?? unknownPlanId; }
  get locked() { return this.unknownPlanId !== null; }
  constructor(private send: typeof fetch = (input, init) => globalThis.fetch(input, init), private storage: StateStore | null = browserStore(), private locks: Pick<LockManager, 'request'> | null = browserLocks()) {}
  private async exclusive<T>(action: () => Promise<T>): Promise<T> {
    if (!this.locks) throw new Error('浏览器不支持可靠的跨页面部署锁；未发送写入请求');
    return this.locks.request('ribbon-deployment-write', { mode: 'exclusive', ifAvailable: true }, async lock => {
      if (!lock) throw new Error('另一页面正在部署；未发送本次写入请求，请先核对原计划');
      if (this.locked) throw new DeploymentUnknown(this.unknownPlanId!);
      return action();
    });
  }
  private pending(planId: string, planDigest: string, kind: string) {
    if (!this.storage) throw new Error('无法持久记录部署计划；未发送写入请求');
    if (pendingId(this.storage)) throw new DeploymentUnknown(pendingId(this.storage)!);
    const record = JSON.stringify({ planId, planDigest, kind, endpoint: kind.startsWith('v1-') ? '/api/v1/deployment/apply' : '/api/v2/deployment/' + kind + '-apply' });
    try { this.storage.setItem(pendingKey, record); if (this.storage.getItem(pendingKey) !== record) throw new Error(); }
    catch { throw new Error('无法持久记录部署计划；未发送写入请求'); }
  }
  private clearPending() { this.storage!.removeItem(pendingKey); if (this.storage!.getItem(pendingKey)) throw new Error('部署状态未清除'); unknownPlanId = null; }
  private async post(path: string, input: unknown, version = 2) {
    const controller = new AbortController(), timer = setTimeout(() => controller.abort(), 30000);
    try {
      const response = await this.send('/api/v' + version + '/deployment/' + path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(input), signal: controller.signal });
      if (!response.ok) {
        let message = '部署请求被拒绝'; try { const body = await response.json(); message = body.message ?? message; } catch {}
        if (response.status < 500 && response.status !== 408) throw new DeploymentRejected(`${response.status} · ${message}`);
        throw new Error(`${response.status} · ${message}`);
      }
      return await response.json();
    } finally { clearTimeout(timer); }
  }
  async preview(kind: 'file' | 'registration', input: { project: RibbonProject; monPath?: string; manifestName?: string; entryName?: string }): Promise<FrozenDeploymentPlan> {
    if (this.locked) throw new DeploymentUnknown(this.unknownPlanId!);
    this.active = null;
    const plan = await this.post(kind + '-preview', input);
    if (plan.contractVersion !== 2 || typeof plan.planId !== 'string' || !/^[a-f0-9]{64}$/.test(plan.planDigest) || typeof plan.expiresAt !== 'string' || !plan.targetPlan || plan.kind !== (kind === 'file' ? 'file-update' : 'registration-add')) throw new Error('冻结预览响应无效，请重新预览');
    this.active = { plan: structuredClone(plan), kind };
    return structuredClone(plan);
  }
  invalidate() { this.active = null; }
  async applyLegacy(input: unknown) { return this.exclusive(() => this.applyLegacyExclusive(input)); }
  private async applyLegacyExclusive(input: unknown) {
    if (this.locked) throw new DeploymentUnknown(this.unknownPlanId!);
    const id = 'v1-first-deployment-' + crypto.randomUUID();
    this.pending(id, 'legacy-no-frozen-digest', 'v1-first-deployment');
    try {
      const receipt = await this.post('apply', input, 1);
      if (!receipt || typeof receipt.unchanged !== 'boolean' || typeof receipt.uicPath !== 'string') throw new Error('v1 写入回执不确定');
      this.clearPending(); return receipt;
    } catch (error) {
      unknownPlanId = id; throw new DeploymentUnknown(id);
    }
  }
  async apply(): Promise<FrozenDeploymentReceipt> { return this.exclusive(() => this.applyExclusive()); }
  private async applyExclusive(): Promise<FrozenDeploymentReceipt> {
    if (this.locked) throw new DeploymentUnknown(this.unknownPlanId!);
    if (!this.active) throw new Error('请先预览');
    const { plan, kind } = this.active; this.active = null;
    this.pending(plan.planId, plan.planDigest, kind);
    try {
      const receipt = await this.post(kind + '-apply', { planId: plan.planId, planDigest: plan.planDigest });
      if (receipt.contractVersion !== 2 || receipt.planId !== plan.planId || receipt.planDigest !== plan.planDigest || !['deployed', 'unchanged'].includes(receipt.state) || receipt.activation?.runtimeVerified !== false || typeof receipt.activation?.reloadRequired !== 'boolean') throw new Error('写入回执不确定');
      this.clearPending(); return receipt;
    } catch (error) {
      if (error instanceof DeploymentRejected) { try { this.clearPending(); } catch { unknownPlanId = plan.planId; throw new DeploymentUnknown(plan.planId); } throw error; }
      unknownPlanId = plan.planId;
      throw new DeploymentUnknown(plan.planId);
    }
  }
}
