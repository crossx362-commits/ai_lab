/** 오너는 명령·채택·반려만 한다. 일을 시키면 안 된다. */

const TASK_AT_OWNER =
  /하세요|해주|직접 열|직접 실|직접 돌|붙여\s*넣|복사해서|열어 보|확인하|설치하|다운로드하|실행하세/;

export function assignsWorkToOwner(text: string) {
  return TASK_AT_OWNER.test(text);
}

export const OWNER_RULE =
  "오너에게 일을 시키지 말 것. 받는 것은 명령·채택·반려 클릭뿐이다. 막히면 조사한다. 질문은 예/아니오일 때만.";
