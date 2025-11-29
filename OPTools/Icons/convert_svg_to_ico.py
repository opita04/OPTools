#!/usr/bin/env python3
"""
Convert SVG to ICO file with multiple sizes
Requires: svglib, reportlab, Pillow
Install with: pip install svglib reportlab Pillow
"""

import os
import sys
from pathlib import Path

try:
    from svglib.svglib import svg2rlg
    from reportlab.graphics import renderPM
    from PIL import Image
    from io import BytesIO
except ImportError as e:
    print(f"Error: Missing required library")
    print("Please install with: pip install svglib reportlab Pillow")
    sys.exit(1)

def svg_to_ico(svg_path, ico_path, sizes=None):
    """Convert SVG to ICO file with multiple sizes"""
    if sizes is None:
        sizes = [16, 32, 48, 64, 96, 128, 256]
    
    images = []
    
    print(f"Converting {svg_path} to ICO with sizes: {sizes}")
    
    # Load SVG once
    try:
        drawing = svg2rlg(svg_path)
        if drawing is None:
            print("Error: Failed to load SVG file")
            sys.exit(1)
    except Exception as e:
        print(f"Error loading SVG: {e}")
        sys.exit(1)
    
    for size in sizes:
        try:
            # Render SVG to PNG bytes at specific size
            # Scale the drawing to the target size
            scale = size / 256.0  # Original SVG is 256x256
            drawing.width = size
            drawing.height = size
            drawing.scale(scale, scale)
            
            # Render to PNG bytes
            png_data = renderPM.drawToString(drawing, fmt='PNG', dpi=96)
            
            # Convert PNG bytes to PIL Image
            img = Image.open(BytesIO(png_data))
            
            # Resize to exact size if needed
            if img.size != (size, size):
                img = img.resize((size, size), Image.Resampling.LANCZOS)
            
            # Convert RGBA to RGB if needed (ICO format requirement)
            if img.mode == 'RGBA':
                # Create transparent background (ICO supports transparency)
                # Keep RGBA for better quality
                pass
            elif img.mode != 'RGB':
                img = img.convert('RGB')
            
            images.append(img)
            print(f"  Created {size}x{size} image")
        except Exception as e:
            print(f"  Warning: Failed to create {size}x{size}: {e}")
            import traceback
            traceback.print_exc()
            continue
    
    if not images:
        print("Error: No images were created")
        sys.exit(1)
    
    # Save as ICO
    try:
        images[0].save(
            ico_path,
            format='ICO',
            sizes=[(img.size[0], img.size[1]) for img in images]
        )
        print(f"Successfully created ICO file: {ico_path}")
    except Exception as e:
        print(f"Error saving ICO file: {e}")
        sys.exit(1)

if __name__ == "__main__":
    script_dir = Path(__file__).parent
    svg_path = script_dir / "chip_icon.svg"
    ico_path = script_dir / "chip_icon.ico"
    
    if not svg_path.exists():
        print(f"Error: SVG file not found: {svg_path}")
        sys.exit(1)
    
    svg_to_ico(str(svg_path), str(ico_path))

