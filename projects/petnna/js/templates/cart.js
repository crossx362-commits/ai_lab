const CART_TEMPLATE = `
<div class="space-y-6">
    <!-- Header/Title section -->
    <div class="flex items-center justify-between border-b pb-4 border-amber-100/50">
        <h2 class="text-xl md:text-2xl font-black text-gray-800 flex items-center gap-2">
            <i class="fa-solid fa-basket-shopping text-brand-500"></i> 안심 장바구니 🛍️
        </h2>
        <button onclick="switchTab('shop')" class="text-xs font-bold text-brand-600 hover:text-brand-700 flex items-center gap-1 bg-brand-50 hover:bg-brand-100 px-3 py-2 rounded-xl transition-all">
            <i class="fa-solid fa-store"></i> 쇼핑 계속하기
        </button>
    </div>

    <!-- Main Content Grid -->
    <div id="cart-page-content">
        <!-- Dynamic content: Empty State or 2-Column Layout -->
    </div>
</div>
`;
