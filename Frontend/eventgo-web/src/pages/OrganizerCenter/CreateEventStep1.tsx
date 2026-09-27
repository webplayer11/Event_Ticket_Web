import React, { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import { OrganizerLayout } from '../../components/organizer/OrganizerLayout';
import { StepIndicator } from '../../components/organizer/StepIndicator';
import { ImageUploadBox } from '../../components/organizer/forms/ImageUploadBox';
import { AddressSection } from '../../components/organizer/forms/AddressSection';
import { RichTextEditorField } from '../../components/organizer/forms/RichTextEditorField';
import { OrganizerInfoSection } from '../../components/organizer/forms/OrganizerInfoSection';

const eventSchema = z.object({
    coverImage: z.any().refine((file) => file, 'Ảnh sự kiện (720x958) là bắt buộc'),
    bgImage: z.any().refine((file) => file, 'Ảnh nền sự kiện (1280x720) là bắt buộc'),
    eventName: z.string().min(1, 'Tên sự kiện là bắt buộc').max(100, 'Tên sự kiện không được vượt quá 100 ký tự'),
    addressType: z.enum(['Offline', 'Online']),
    locationName: z.string().max(80, 'Tên địa điểm không được vượt quá 80 ký tự').optional(),
    province: z.string().optional(),
    ward: z.string().optional(),
    address: z.string().max(80, 'Số nhà, đường không được vượt quá 80 ký tự').optional(),
    onlineLink: z.string().url('Link không hợp lệ').optional(),
    category: z.string().min(1, 'Vui lòng chọn thể loại sự kiện'),
    description: z.string().min(1, 'Thông tin sự kiện là bắt buộc').refine((val) => {
        // Very basic check that it's not just empty HTML tags
        const str = val.replace(/(<([^>]+)>)/gi, "").trim();
        return str.length > 0;
    }, 'Thông tin sự kiện không được để trống'),
    organizerLogo: z.any().refine((file) => file, 'Logo ban tổ chức là bắt buộc'),
    organizerName: z.string().min(1, 'Tên ban tổ chức là bắt buộc').max(80, 'Tên ban tổ chức không được vượt quá 80 ký tự'),
    organizerInfo: z.string().min(1, 'Thông tin ban tổ chức là bắt buộc').max(500, 'Thông tin ban tổ chức không được vượt quá 500 ký tự'),
}).superRefine((data, ctx) => {
    if (data.addressType === 'Offline') {
        if (!data.locationName) ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Tên địa điểm là bắt buộc', path: ['locationName'] });
        if (!data.province) ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Tỉnh/Thành là bắt buộc', path: ['province'] });
        if (!data.address) ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Số nhà, đường là bắt buộc', path: ['address'] });
    } else {
        if (!data.onlineLink) ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Link tham gia là bắt buộc', path: ['onlineLink'] });
    }
});

type EventFormValues = z.infer<typeof eventSchema>;

const initialDescription = `<h3>Giới thiệu sự kiện</h3>
<p>[Tóm tắt ngắn gọn về sự kiện: Nội dung chính của sự kiện, điểm đặc sắc nhất và lý do khiến người tham gia không nên bỏ lỡ]</p>
<h3>Chi tiết sự kiện</h3>
<ul>
  <li><strong>Chương trình chính:</strong> [Liệt kê những hoạt động nổi bật trong sự kiện...]</li>
  <li><strong>Khách mời:</strong> [Thông tin về các khách mời đặc biệt...]</li>
  <li><strong>Trải nghiệm đặc biệt:</strong> [Nếu có các hoạt động đặc biệt khác...]</li>
</ul>
<h3>Điều khoản và điều kiện</h3>
<p>[TnC] sự kiện<br>Lưu ý về điều khoản trẻ em<br>Lưu ý về điều khoản VAT</p>`;

export const CreateEventStep1 = () => {
    const {
        register,
        handleSubmit,
        watch,
        setValue,
        formState: { errors },
    } = useForm<EventFormValues>({
        resolver: zodResolver(eventSchema),
        defaultValues: {
            addressType: 'Offline',
            description: initialDescription,
            category: '',
            province: '',
            ward: '',
        },
    });

    const addressType = watch('addressType');
    const province = watch('province');

    // For character counts
    const eventName = watch('eventName') || '';
    const locationName = watch('locationName') || '';
    const address = watch('address') || '';
    const organizerName = watch('organizerName') || '';
    const organizerInfo = watch('organizerInfo') || '';

    const onSubmit = (data: EventFormValues) => {
        console.log("Form Submitted:", data);
        alert("Dữ liệu hợp lệ! Vui lòng kiểm tra console để xem chi tiết.\n(Sẽ chuyển qua Bước 2)");
    };

    const processDraft = () => {
        console.log("Draft saved!");
        alert("Đã lưu bản nháp thành công!");
    };

    return (
        <OrganizerLayout>
            <div className="mx-auto max-w-4xl py-8">
                <StepIndicator currentStep={1} onSave={processDraft} onContinue={handleSubmit(onSubmit)} />

                <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-8 pb-32">
                    {/* Upload ảnh */}
                    <div className="flex flex-col gap-4 md:flex-row">
                        <div className="w-full md:w-[35%]">
                            <ImageUploadBox
                                label="Thêm ảnh sự kiện để hiển thị ở các vị trí khác (720x958)"
                                dimensionLabel="720x958"
                                orientation="portrait"
                                registration={register('coverImage')}
                                error={errors.coverImage?.message as string}
                                required
                            />
                        </div>
                        <div className="w-full md:w-[65%]">
                            <ImageUploadBox
                                label="Thêm ảnh nền sự kiện (1280x720)"
                                dimensionLabel="1280x720"
                                orientation="landscape"
                                registration={register('bgImage')}
                                error={errors.bgImage?.message as string}
                                required
                            />
                        </div>
                    </div>

                    {/* Tên sự kiện */}
                    <div className="flex flex-col gap-2 rounded-lg border border-[#2d2d2d] bg-[#12181A] p-6">
                        <label className="text-sm font-medium text-white">
                            Tên sự kiện <span className="text-green-500">*</span>
                        </label>
                        <div className="relative">
                            <input
                                type="text"
                                placeholder="Tên sự kiện"
                                className={`w-full rounded-md border text-black bg-white px-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500 ${errors.eventName ? 'border-red-500' : 'border-gray-300'
                                    }`}
                                {...register('eventName')}
                            />
                            <span className={`absolute right-3 top-2.5 text-xs ${eventName.length >= 100 ? 'text-red-500 font-bold' : 'text-gray-400'}`}>
                                {eventName.length} / 100
                            </span>
                        </div>
                        {errors.eventName && <p className="text-xs text-red-500">{errors.eventName.message}</p>}
                    </div>

                    {/* Địa điểm */}
                    <AddressSection
                        type={addressType}
                        onChangeType={(type) => setValue('addressType', type)}
                        registers={{
                            locationName: register('locationName'),
                            province: register('province'),
                            ward: register('ward'),
                            address: register('address'),
                            onlineLink: register('onlineLink'),
                        }}
                        errors={errors}
                        provinceValue={province}
                        charCounts={{
                            locationName: locationName.length,
                            address: address.length,
                        }}
                    />

                    {/* Thể loại sự kiện */}
                    <div className="flex flex-col gap-2 rounded-lg border border-[#2d2d2d] bg-[#12181A] p-6">
                        <label className="text-sm font-medium text-white">
                            Thể loại sự kiện <span className="text-green-500">*</span>
                        </label>
                        <select
                            className={`w-full rounded-md border text-black bg-white px-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-green-500 ${errors.category ? 'border-red-500' : 'border-gray-300'
                                }`}
                            {...register('category')}
                        >
                            <option value="" disabled>Vui lòng chọn</option>
                            <option value="Âm nhạc">Âm nhạc</option>
                            <option value="Thể thao">Thể thao</option>
                            <option value="Hội thảo/Workshop">Hội thảo/Workshop</option>
                            <option value="Sân khấu & Nghệ thuật">Sân khấu & Nghệ thuật</option>
                            <option value="Khác">Khác...</option>
                        </select>
                        {errors.category && <p className="text-xs text-red-500">{errors.category.message}</p>}
                    </div>

                    {/* Thông tin sự kiện (RichTextEditor) */}
                    <RichTextEditorField
                        value={watch('description')}
                        onChange={(val) => setValue('description', val, { shouldValidate: true })}
                        error={errors.description?.message}
                    />

                    {/* Organizer Info */}
                    <OrganizerInfoSection
                        registers={{
                            logo: register('organizerLogo'),
                            name: register('organizerName'),
                            info: register('organizerInfo'),
                        }}
                        errors={errors}
                        charCounts={{
                            name: organizerName.length,
                            info: organizerInfo.length,
                        }}
                    />
                </form>
            </div>
        </OrganizerLayout>
    );
};
