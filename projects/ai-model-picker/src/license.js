// 서버 전용 모듈 — node:crypto와 서명 시크릿을 쓴다. 브라우저에서 import 하지 않는다.
//
// 라이선스 키 = "<PayPal 주문ID>-<HMAC 앞 10자리>".
// 서버가 시크릿으로 서명을 재계산해 대조하므로 구매 기록을 저장할 필요가 없다.
// 트레이드오프: 환불된 구매의 키는 자동으로 죽지 않는다(설계 문서 §4 참고).
import { createHmac, timingSafeEqual } from 'node:crypto';

const SIG_LENGTH = 10;
const INVALID = 'That license key is not valid.';

function signature(orderId, secret) {
  return createHmac('sha256', secret).update(orderId).digest('hex').slice(0, SIG_LENGTH).toUpperCase();
}

export function signLicense(orderId, secret) {
  const id = String(orderId ?? '').trim().toUpperCase();
  if (!id || !secret) throw new Error('signLicense requires an order id and a secret');
  return `${id}-${signature(id, secret)}`;
}

export function verifyLicense(key, secret) {
  const fail = (reason = INVALID) => ({ valid: false, reason, orderId: '' });
  if (typeof key !== 'string' || !secret) return fail();

  const normalized = key.trim().toUpperCase();
  const split = normalized.lastIndexOf('-');
  if (split <= 0 || split === normalized.length - 1) return fail();

  const orderId = normalized.slice(0, split);
  const provided = normalized.slice(split + 1);
  const expected = signature(orderId, secret);
  // timingSafeEqual은 길이가 다르면 던진다 — 먼저 길이를 본다.
  if (provided.length !== expected.length) return fail();
  if (!timingSafeEqual(Buffer.from(provided), Buffer.from(expected))) return fail();

  return { valid: true, reason: '', orderId };
}
