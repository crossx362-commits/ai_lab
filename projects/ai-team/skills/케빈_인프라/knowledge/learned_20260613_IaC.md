---
learned_at: 2026-06-13T02:00:52.118672
agent: 케빈
topic: IaC
---

# IaC

## 핵심 개념
* **코드 기반 인프라:** 인프라 설정을 코드로 관리 (Declarative & Imperative).
* **선언적 vs 명령적:** 선언적(Desired State) vs 명령적(Step-by-Step) 방식의 차이 이해.
* **버전 관리:** Git 등을 활용한 변화 추적 및 롤백 용이성.
* **자동화:** 인프라 프로비저닝, 구성 관리 자동화.

## 실전 적용
* **Terraform:** HCL(HashiCorp Configuration Language)을 사용하여 AWS, Azure 등 클라우드 인프라 코딩. `terraform apply` 명령으로 적용.
* **Ansible:** YAML 기반으로 서버 설정 관리. Playbook 작성 및 실행. `ansible-playbook my_playbook.yml`
* **CloudFormation (AWS):** JSON 또는 YAML로 AWS 리소스 정의. `aws cloudformation deploy --template-file template.yaml`

## 케빈에게 유용한 이유
* **DevOps 효율성 증대:** CI/CD 파이프라인 자동화 및 인프라 변경 관리 효율화.
* **서버 모니터링 연동:** IaC를 통해 모니터링 에이전트 자동 배포 및 설정.
* **인프라 일관성 유지:** 환경별 일관된 인프라 구축 및 관리 용이.
