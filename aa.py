import requests
import random

def generate_text(prompt, system_prompt=""):
    url = "https://text.pollinations.ai"
    data = {
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": prompt}
        ],
        "model": "openai-fast",
        "seed": random.randint(1, 999999999),
        "jsonMode": False,
        "private": True,
        "stream": False
    }
    
    response = requests.post(url, json=data)
    if response.status_code == 200:
        return response.text
    return "Failed to generate text."

# Test the function
print(generate_text("Give me a one-liner inspirational quote."))