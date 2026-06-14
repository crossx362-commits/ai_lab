import requests
from bs4 import BeautifulSoup
import json

url = "https://swiftcart-101711.myshopify.com/"
headers = {"User-Agent": "Mozilla/5.0"}

try:
    r = requests.get(url, headers=headers, timeout=15)
    soup = BeautifulSoup(r.text, "html.parser")
    
    # Print headings
    print("=== HEADINGS ===")
    for h in soup.find_all(["h1", "h2", "h3"]):
        text = h.get_text(strip=True)
        if text:
            print(f"- {h.name}: {text}")
            
    # Find all product titles, prices
    print("\n=== PRODUCTS ===")
    # Dawn theme uses class product-card, card__heading, or custom grid classes
    # Let's search for anything containing price or product
    products = []
    for card in soup.find_all(class_=lambda c: c and ("card" in c or "product" in c)):
        title_el = card.find(class_=lambda c: c and "title" in c or "heading" in c)
        price_el = card.find(class_=lambda c: c and "price" in c)
        if title_el:
            title = title_el.get_text(strip=True)
            price = price_el.get_text(strip=True) if price_el else "N/A"
            if title and title not in [p["title"] for p in products]:
                products.append({"title": title, "price": price})
                
    for p in products[:15]:
        print(f"Product: {p['title']} | Price: {p['price']}")
        
except Exception as e:
    print("Error:", e)
