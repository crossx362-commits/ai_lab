---
learned_at: 2026-06-11T16:02:22.282820
agent: 케빈
topic: IaC
---

# IaC

## 핵심 개념

* **코드로서의 인프라:** 인프라 구축 및 관리 과정을 코드로 정의하여 버전 관리, 자동화, 재사용성을 높입니다.
* **선언적 vs. 명령적:** 선언적 IaC는 원하는 상태를 정의하고, 명령적 IaC는 상태를 달성하는 단계를 정의합니다. (선언적 권장)
* **Immutable 인프라:** 변경 시 새로운 인스턴스를 생성하고 기존 인스턴스를 대체하여 일관성을 유지합니다.
* **Idempotence:** 동일한 코드를 반복 실행해도 결과가 동일하게 유지되는 속성.
* **스테이트 관리:** IaC 적용 상태를 기록하고 관리하여 변경 추적 및 롤백을 용이하게 합니다.

## 실전 적용

Terraform을 사용하여 AWS EC2 인스턴스를 생성하는 예시:

```terraform
resource "aws_instance" "example" {
  ami           = "ami-xxxxxxxxxxxxx"
  instance_type = "t2.micro"
  tags = {
    Name = "Example Instance"
  }
}
```

`terraform init`, `terraform plan`, `terraform apply` 명령어를 사용하여 적용합니다.

## 케빈에게 유용한 이유

인프라 관리 작업의 자동화, 일관성 유지, 오류 감소, 배포 시간 단축에 기여합니다. DevOps 파이프라인 통합 및 CI/CD 효율성 향상에 필수적이며, 서버 모니터링 환경 구축 자동화에도 활용 가능합니다.
