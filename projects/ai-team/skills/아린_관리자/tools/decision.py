import json

def make_decision(analysis_result: dict) -> str:
    """Generate a simple decision string from analysis result.
    For the simple flow we just summarize whether any insights were found.
    """
    insights = analysis_result.get("insights", [])
    if insights:
        return "Proceed with posting – insights found."
    else:
        return "Proceed with posting – no specific insights."
