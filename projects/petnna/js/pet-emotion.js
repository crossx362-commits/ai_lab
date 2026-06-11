// pet-emotion.js — 펫 감정 표현 시스템 (루나)

// 시간대별 자동 인사 생성
function getTimeBasedGreeting() {
    const hour = new Date().getHours();
    const pet = getActivePet();
    if (!pet) return "집사님, 안녕하세요! 🐾";

    const petName = pet.name || "댕이";
    const mbti = pet.mbtiCode || "";

    if (hour >= 5 && hour < 9) {
        // 아침 (05:00-09:00)
        const morningGreetings = [
            `${petName}: 집사님, 좋은 아침이에요! 오늘도 힘내세요! ☀️`,
            `${petName}: 하암~ 잘 잤어요? 저는 꿈에서 산책했어요! 🌅`,
            `${petName}: 아침 밥 시간이에요! 배고파요~ 🍚`
        ];
        return morningGreetings[Math.floor(Math.random() * morningGreetings.length)];
    } else if (hour >= 9 && hour < 12) {
        // 오전 (09:00-12:00)
        return `${petName}: 날씨 좋은데 산책 어때요? 🚶‍♂️`;
    } else if (hour >= 12 && hour < 14) {
        // 점심 (12:00-14:00)
        return `${petName}: 점심 맛있게 드세요! 저도 간식 하나 주세요~ 🍖`;
    } else if (hour >= 14 && hour < 18) {
        // 오후 (14:00-18:00)
        const afternoonGreetings = [
            `${petName}: 졸려요... 낮잠 타임이에요 💤`,
            `${petName}: 집사님, 같이 놀아요! 심심해요~ 🎾`,
            `${petName}: 오후에는 간식이 필요해요! ✨`
        ];
        return afternoonGreetings[Math.floor(Math.random() * afternoonGreetings.length)];
    } else if (hour >= 18 && hour < 22) {
        // 저녁 (18:00-22:00)
        return `${petName}: 저녁 시간이에요! 오늘 하루도 고생하셨어요! 🌙`;
    } else {
        // 밤 (22:00-05:00)
        return `${petName}: 이제 잘 시간이에요. 편안한 밤 되세요~ 😴`;
    }
}

// 돌봄 완료 시 피드백 메시지
function getCareCompletionFeedback(careType) {
    const pet = getActivePet();
    if (!pet) return "고마워요!";

    const petName = pet.name || "댕이";
    const feedbacks = {
        '산책': [
            `${petName}: 와! 산책 즐거웠어요! 다음에 또 가요! 🐕`,
            `${petName}: 신난다! 밖에서 노니까 행복해요~ 🌳`,
            `${petName}: 산책은 언제나 최고예요! 감사해요! 🐾`
        ],
        '밥': [
            `${petName}: 냠냠! 맛있었어요! 배가 든든해요 🍚`,
            `${petName}: 고마워요! 배고팠는데 잘 먹었어요! 😋`,
            `${petName}: 밥 먹으니까 힘이 불끈! 💪`
        ],
        '간식': [
            `${petName}: 간식 최고! 집사님 사랑해요! 💕`,
            `${petName}: 와아! 제가 제일 좋아하는 거예요! 🎉`,
            `${petName}: 맛있다! 또 주세요~ 🥩`
        ],
        '목욕': [
            `${petName}: 뽀송뽀송! 깨끗해진 기분이에요~ 🛁`,
            `${petName}: 목욕은 싫지만... 깨끗해서 좋아요! ✨`,
            `${petName}: 이제 향기나요! 냄새 맡아보세요! 🌸`
        ],
        '놀이': [
            `${petName}: 신나게 놀았어요! 너무 재밌었어요! 🎾`,
            `${petName}: 집사님이랑 노는 게 제일 좋아요! 😄`,
            `${petName}: 아직도 더 놀고 싶어요! 🥎`
        ],
        '병원': [
            `${petName}: 병원은 무서웠지만 이제 괜찮아요! 💉`,
            `${petName}: 건강 체크 완료! 저 건강해요! 🏥`,
            `${petName}: 수의사 선생님이 칭찬해주셨어요! 😊`
        ],
        '미용': [
            `${petName}: 예뻐졌나요? 헤헤~ 💇`,
            `${petName}: 미용 끝! 이제 멋쟁이예요! ✂️`,
            `${petName}: 털이 짧아져서 시원해요! 🌬️`
        ]
    };

    const messages = feedbacks[careType] || [`${petName}: 고마워요! 집사님! 💖`];
    return messages[Math.floor(Math.random() * messages.length)];
}

// 감정 상태 기반 메시지 (배고픔/행복도)
function getEmotionBasedMessage() {
    const pet = getActivePet();
    if (!pet) return null;

    const petName = pet.name || "댕이";

    // 배고픔 경고
    if (pet.hunger < 30) {
        return `${petName}: 배가 너무 고파요... 밥 주세요~ 😢`;
    }

    // 행복도 저하 경고
    if (pet.happy < 40) {
        return `${petName}: 심심해요... 같이 놀아주세요! 🥺`;
    }

    // 건강 상태 좋음
    if (pet.hunger > 80 && pet.happy > 80) {
        return `${petName}: 배도 부르고 행복해요! 최고예요! 🌟`;
    }

    return null;
}

// 펫 대사 자동 갱신 (1시간마다)
function initPetEmotionSystem() {
    // 처음 로드 시 시간대별 인사
    const greeting = getTimeBasedGreeting();
    const pet = getActivePet();
    if (pet && greeting) {
        pet.tempSpeechText = greeting;
    }

    // 1시간마다 새로운 인사로 갱신
    setInterval(() => {
        const currentPet = getActivePet();
        if (currentPet) {
            // 감정 기반 메시지가 있으면 우선
            const emotionMsg = getEmotionBasedMessage();
            if (emotionMsg) {
                currentPet.tempSpeechText = emotionMsg;
            } else {
                currentPet.tempSpeechText = getTimeBasedGreeting();
            }

            if (typeof renderMyPets === 'function') {
                renderMyPets();
            }
        }
    }, 60 * 60 * 1000); // 1시간

    // 10분마다 감정 체크
    setInterval(() => {
        const currentPet = getActivePet();
        if (currentPet) {
            const emotionMsg = getEmotionBasedMessage();
            if (emotionMsg) {
                currentPet.tempSpeechText = emotionMsg;
                if (typeof renderMyPets === 'function') {
                    renderMyPets();
                }
            }
        }
    }, 10 * 60 * 1000); // 10분
}

// 돌봄 액션 완료 시 호출 (다른 파일에서 사용)
function triggerCareFeedback(careType) {
    const pet = getActivePet();
    if (pet) {
        const feedback = getCareCompletionFeedback(careType);
        pet.tempSpeechText = feedback;
        pet.happy = Math.min(100, pet.happy + 5); // 감사 표현 시 행복도 +5

        if (typeof saveState === 'function') saveState();
        if (typeof renderMyPets === 'function') renderMyPets();
    }
}
