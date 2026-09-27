import React, { useState } from 'react';
import { UploadCloud, X } from 'lucide-react';
import { UseFormRegisterReturn } from 'react-hook-form';

interface ImageUploadBoxProps {
    label: string;
    dimensionLabel: string;
    orientation?: 'portrait' | 'landscape' | 'square';
    registration?: UseFormRegisterReturn;
    onChange?: (file: File | null) => void;
    error?: string;
    required?: boolean;
}

export const ImageUploadBox: React.FC<ImageUploadBoxProps> = ({
    label,
    dimensionLabel,
    orientation = 'landscape',
    registration,
    onChange,
    error,
    required,
}) => {
    const [preview, setPreview] = useState<string | null>(null);

    const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const file = e.target.files?.[0];
        if (registration && registration.onChange) {
            registration.onChange(e);
        }
        if (file) {
            setPreview(URL.createObjectURL(file));
            if (onChange) onChange(file);
        }
    };

    const clearImage = (e: React.MouseEvent) => {
        e.preventDefault();
        setPreview(null);
        if (onChange) onChange(null);
        // In a real implementation we would also clear the form value
    };

    const getAspectRatio = () => {
        switch (orientation) {
            case 'portrait':
                return 'aspect-[3/4]';
            case 'square':
                return 'aspect-square';
            default:
                return 'aspect-video';
        }
    };

    return (
        <div className="flex w-full flex-col gap-2">
            <div className="flex items-center justify-between">
                <label className="text-sm font-medium text-white">
                    {label} {required && <span className="text-green-500">*</span>}
                </label>
                {orientation !== 'square' && (
                    <a href="#" className="text-xs text-green-500 hover:underline">
                        Xem vị trí hiển thị các ảnh
                    </a>
                )}
            </div>

            <div
                className={`relative flex ${getAspectRatio()} w-full cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed ${error ? 'border-red-500 bg-red-500/10' : 'border-gray-600 bg-[#1A1F24]'
                    } transition-colors hover:border-green-500 hover:bg-[#20262C]`}
            >
                <input
                    type="file"
                    accept="image/*"
                    className="absolute inset-0 h-full w-full cursor-pointer opacity-0"
                    {...registration}
                    onChange={handleFileChange}
                />

                {preview ? (
                    <div className="absolute inset-0 h-full w-full">
                        <img
                            src={preview}
                            alt="Preview"
                            className="h-full w-full rounded-lg object-cover"
                        />
                        <button
                            onClick={clearImage}
                            className="absolute right-2 top-2 z-10 rounded-full bg-black/60 p-1 text-white hover:bg-red-500 transition-colors"
                        >
                            <X className="h-4 w-4" />
                        </button>
                        <div className="absolute inset-0 z-0 bg-black/40 opacity-0 transition-opacity hover:opacity-100 rounded-lg flex items-center justify-center pointer-events-none">
                            <span className="text-white font-medium">Thay đổi ảnh</span>
                        </div>
                    </div>
                ) : (
                    <div className="flex flex-col items-center gap-2 p-4 text-center">
                        <div className="rounded-full bg-gray-800 p-3">
                            <UploadCloud className="h-6 w-6 text-green-500" />
                        </div>
                        <p className="text-sm text-gray-400">{dimensionLabel}</p>
                    </div>
                )}
            </div>
            {error && <p className="text-xs text-red-500">{error}</p>}
        </div>
    );
};
