import os
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import matplotlib.patches as patches

OUTPUT_IMG_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "c4_figures")
os.makedirs(OUTPUT_IMG_DIR, exist_ok=True)

# Set default font to Times New Roman if available, otherwise serif
plt.rcParams['font.family'] = 'serif'
plt.rcParams['font.serif'] = ['Times New Roman', 'DejaVu Serif', 'Liberation Serif']
plt.rcParams['mathtext.fontset'] = 'stix'

print("Matplotlib initialized for academic C4 diagram generation.")
